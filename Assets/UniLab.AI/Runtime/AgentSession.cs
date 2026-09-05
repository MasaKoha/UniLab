#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>LLM エージェントの 1 セッション分の状態です。判断を外部に置き、Unity 側を観測・入力・記録の器に限定するために使います。</summary>
    public sealed class AgentSession : IDisposable
    {
        private readonly AgentGoal _goal;
        private readonly AgentExpectationEvaluator _evaluator = new AgentExpectationEvaluator();
        private readonly AgentActionExecutor _executor;
        private readonly AgentObservationFormatter _formatter;
        private readonly AgentSessionArtifacts _artifacts;
        private readonly AgentSessionGuards _guards;
        private AgentSessionDriver _driver;
        private ExceptionForensics _ownedForensics;
        private ExceptionForensics _forensics;
        private UiSnapshotDocument _lastSnapshot;
        private string _result;
        private string _message;
        private bool _ended;
        private bool _disposed;
        private bool _sessionStateEntered;

        private AgentSession(AgentGoal goal, AgentOptions options)
        {
            _goal = goal ?? new AgentGoal();
            var sessionOptions = options ?? new AgentOptions();
            _guards = new AgentSessionGuards(_goal, sessionOptions);
            _artifacts = new AgentSessionArtifacts(_goal, sessionOptions, _guards, _evaluator);
            _executor = new AgentActionExecutor(EnsureDriver);
            _formatter = new AgentObservationFormatter(_goal, sessionOptions, _evaluator, () => IsInputBusy);
            _result = "running";
            _message = string.Empty;

            _artifacts.Initialize();
            EnsureDriver();
            InitializeForensics();
            SaveSessionReport();
        }

        /// <summary>新しいセッションを開始し、LLM が参照する成果物ディレクトリを即作成します。</summary>
        public static AgentSession Begin(AgentGoal goal, AgentOptions options)
        {
            var session = new AgentSession(goal, options);
            session.EnterSessionState();
            return session;
        }

        /// <summary>継続入力（hold / stick / drag 等）がまだ進行中なら true。落ち着き待ちの判断材料にする。</summary>
        public bool IsInputBusy
        {
            get { return _driver != null && _driver.IsBusy; }
        }

        /// <summary>セッション識別子を返し、外部ログと DebugOutput の対応を固定します。</summary>
        public string SessionId
        {
            get { return _artifacts.SessionId; }
        }

        /// <summary>セッション成果物のディレクトリを返し、外部運転手が追記や回収に使えるようにします。</summary>
        public string OutputDirectory
        {
            get { return _artifacts.OutputDirectory; }
        }

        /// <summary>直近の状態メッセージです。ExportAsScenario 拒否理由を外部コマンド応答へそのまま返すために公開します。</summary>
        public string StatusMessage
        {
            get { return _message ?? string.Empty; }
        }

        /// <summary>現在の観測を AI 向け圧縮テキストで返します。差分だけを選べるようにして、長いプレイのトークン消費を抑えます。</summary>
        public string Observe(bool diffOnly, string scope = "visible")
        {
            UiObservationScope.Validate(scope);
            var snapshot = UiSnapshot.Capture();
            var text = diffOnly && _lastSnapshot != null
                ? _formatter.BuildDiffObservation(_lastSnapshot, snapshot, scope)
                : _formatter.BuildFullObservation(snapshot, scope);
            _lastSnapshot = snapshot;
            return text;
        }

        /// <summary>1 手を実行し、拒否理由や詰み判定を同じテキストで返します。外部 LLM が次の判断へ失敗情報を戻せるようにするためです。</summary>
        public string Act(AgentAction action)
        {
            if (_ended)
            {
                return _formatter.BuildStatusText("ended", "セッションは終了済みです。", Observe(false));
            }

            if (_guards.IsStepBudgetExceeded(_artifacts.StepCount))
            {
                Finish("maxSteps", "手数上限に到達しました。");
                return _formatter.BuildStatusText(_result, _message, Observe(false));
            }

            if (_guards.IsTimeBudgetExceeded(_artifacts.StartedAtRealtime, Time.realtimeSinceStartupAsDouble))
            {
                Finish("maxSeconds", "実時間上限に到達しました。");
                return _formatter.BuildStatusText(_result, _message, Observe(false));
            }

            var beforeSnapshot = UiSnapshot.Capture();
            var beforeObservationKey = UiSnapshot.ToCompactText(beforeSnapshot);
            var actionKind = AgentActionExecutor.GetActionKind(action);
            var target = AgentActionExecutor.GetActionTarget(action);
            var actionKey = AgentActionExecutor.BuildActionKey(action);
            if (_guards.IsForbidden(actionKey, target))
            {
                return RejectAction(action, beforeObservationKey, actionKind, target, "forbid に一致するため拒否しました。");
            }

            if (!_goal.freePlay && _guards.IsStuck(beforeObservationKey, actionKey))
            {
                Finish("stuck", "同じ観測または同じ観測で同じ行動が反復したため停止しました。");
                _artifacts.SaveAbnormalCapture("stuck");
                _artifacts.AppendActionLog(action, beforeObservationKey, actionKind, target, "stuck", _message, string.Empty);
                return _formatter.BuildStatusText(_result, _message, _formatter.BuildFullObservation(beforeSnapshot));
            }

            var forensicsStartCount = _forensics == null ? 0 : _forensics.CapturedCount;
            var message = string.Empty;
            try
            {
                message = _executor.ExecuteAction(action);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                _artifacts.SaveAbnormalCapture("exception");
                message = $"例外発生: {exception.GetType().Name} {exception.Message}";
            }

            if (_forensics != null && _forensics.CapturedCount > forensicsStartCount)
            {
                message = $"{message} / 例外フォレンジックを保存しました。";
            }

            _artifacts.RecordScenarioStep(action);
            var afterSnapshot = UiSnapshot.Capture();
            var diff = UiSnapshot.Compare(beforeSnapshot, afterSnapshot);
            var diffText = _formatter.FormatDiff(diff);
            _lastSnapshot = afterSnapshot;
            _artifacts.AppendActionLog(action, beforeObservationKey, actionKind, target, "acted", message, diffText);

            if (IsGoalReached(afterSnapshot, diff))
            {
                Finish("reached", "目標を達成しました。");
            }

            SaveSessionReport();
            return _formatter.BuildStatusText(_result, message, _formatter.BuildFullObservation(afterSnapshot));
        }

        /// <summary>目標条件を 02 の expect 語彙で評価し、LLM の自己申告を成功条件にしないようにします。</summary>
        public bool IsGoalReached()
        {
            var previousSnapshot = _lastSnapshot;
            var latestSnapshot = UiSnapshot.Capture();
            var diff = previousSnapshot == null ? null : UiSnapshot.Compare(previousSnapshot, latestSnapshot);
            _lastSnapshot = latestSnapshot;
            var isReached = IsGoalReached(latestSnapshot, diff);
            SaveSessionReport();
            return isReached;
        }

        /// <summary>実行されなかった手の評価を前の手へ混入させないための記録位置です。</summary>
        internal int RecordedStepCount => _artifacts.StepCount;

        /// <summary>書き出したシナリオの集計です。</summary>
        internal string ExportSummary => _artifacts.ExportSummary;

        /// <summary>ゲートウェイが入力安定後に評価した結果を保存対象へ戻します。</summary>
        internal void RecordActExpectation(int previousStepCount, bool expectOk)
        {
            _artifacts.RecordActExpectation(previousStepCount, expectOk);
        }

        /// <summary>成功した手順を 02 のシナリオ JSON として書き出し、探索結果を再実行可能なテストへ昇格します。</summary>
        public string ExportAsScenario(string name)
        {
            if (!_goal.freePlay && !IsGoalReached())
            {
                _message = $"目標未達のため scenario.json は書き出しません。 {_formatter.BuildGoalFailureSummary()}";
                _artifacts.SaveAbnormalCapture("goal-failed");
                SaveSessionReport();
                return string.Empty;
            }

            var scenarioFilePath = _artifacts.ExportAsScenario(name);
            SaveSessionReport();
            return scenarioFilePath;
        }

        /// <summary>セッションを明示終了し、外部運転手が成果物の確定時点を判断できるようにします。</summary>
        public void End()
        {
            if (_ended)
            {
                ExitSessionStateIfNeeded();
                return;
            }

            Finish("ended", "外部から終了されました。");
            ExitSessionStateIfNeeded();
        }

        /// <summary>ドライバと入力状態を破棄し、継続入力が次の検証へ残らないようにします。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ExitSessionStateIfNeeded();
            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);
                _driver = null;
            }

            _ownedForensics?.Dispose();
            _ownedForensics = null;
            _forensics = null;
            InputInjector.Dispose();
        }

        private void InitializeForensics()
        {
            _forensics = ExceptionForensics.Current;
            if (_forensics != null)
            {
                return;
            }

            _ownedForensics = new ExceptionForensics();
            _ownedForensics.Initialize(_artifacts.ForensicsDirectory);
            _forensics = _ownedForensics;
        }

        private bool IsGoalReached(UiSnapshotDocument snapshot, UiSnapshotDiff diff)
        {
            return !_goal.freePlay && _evaluator.Evaluate(_goal.goal, snapshot, diff);
        }

        private string RejectAction(AgentAction action, string observationKey, string actionKind, string target, string message)
        {
            _artifacts.AppendActionLog(action, observationKey, actionKind, target, "rejected", message, string.Empty);
            SaveSessionReport();
            return _formatter.BuildStatusText("rejected", message, _formatter.BuildFullObservation(_lastSnapshot ?? UiSnapshot.Capture()));
        }

        private AgentSessionDriver EnsureDriver()
        {
            if (_driver != null)
            {
                return _driver;
            }

            var driverObject = new GameObject(nameof(AgentSessionDriver));
            UnityEngine.Object.DontDestroyOnLoad(driverObject);
            _driver = driverObject.AddComponent<AgentSessionDriver>();
            return _driver;
        }

        private void SaveSessionReport()
        {
            _artifacts.SaveSessionReport(_result, _ended, _message, _lastSnapshot);
        }

        private void Finish(string result, string message)
        {
            _result = result;
            _message = message;
            _ended = true;
            SaveSessionReport();
        }

        private void EnterSessionState()
        {
            if (_sessionStateEntered)
            {
                return;
            }

            _sessionStateEntered = true;
            AiSessionState.Enter("agent");
        }

        private void ExitSessionStateIfNeeded()
        {
            if (!_sessionStateEntered)
            {
                return;
            }

            _sessionStateEntered = false;
            AiSessionState.Exit("agent");
        }
    }
}
#endif
