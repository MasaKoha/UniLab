#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace UniLab.AI
{
    /// <summary>
    /// LLM エージェントの 1 セッション分の状態です。
    /// 判断を外部に置き、Unity 側を観測・入力・記録の器に限定するために使います。
    /// </summary>
    public sealed class AgentSession : IDisposable
    {
        private const string AgentDirectoryName = "agent";
        private const string SessionFileName = "session.json";
        private const string ActionsFileName = "actions.jsonl";
        private const string ScenarioFileName = "scenario.json";
        private const string AbnormalCapturePrefix = "abnormal-";
        private const string CaptureFileExtension = ".png";
        private const string SessionTimestampFormat = "yyyyMMdd-HHmmss";
        private const int DefaultMaxSteps = 200;
        private const int DefaultMaxSeconds = 600;
        private const int DefaultStuckRepeatLimit = 3;
        private const int DefaultSettleFrames = 1;
        private const float DefaultContinuousSeconds = 0.1f;

        private readonly AgentGoal _goal;
        private readonly AgentOptions _options;
        private readonly AgentExpectationEvaluator _evaluator = new AgentExpectationEvaluator();
        private readonly List<AgentActionLogEntry> _actionLogs = new List<AgentActionLogEntry>();
        private readonly List<UiScenarioStep> _scenarioSteps = new List<UiScenarioStep>();
        private readonly string _sessionId;
        private readonly string _outputDirectory;
        private readonly string _sessionFilePath;
        private readonly string _actionsFilePath;
        private readonly string _startedAtText;
        private readonly double _startedAtRealtime;
        private AgentSessionDriver _driver;
        private ExceptionForensics _ownedForensics;
        private ExceptionForensics _forensics;
        private UiSnapshotDocument _lastSnapshot;
        private string _lastObservationKey;
        private string _lastRepeatedObservationKey;
        private string _lastActionKey;
        private string _result;
        private string _message;
        private string _scenarioFilePath;
        private int _sameObservationActionCount;
        private int _sameObservationCount;
        private bool _ended;
        private bool _disposed;

        private AgentSession(AgentGoal goal, AgentOptions options)
        {
            _goal = goal ?? new AgentGoal();
            _options = options ?? new AgentOptions();
            _sessionId = DateTime.Now.ToString(SessionTimestampFormat, CultureInfo.InvariantCulture);
            _outputDirectory = Path.Combine(DebugOutputPath.DirectoryPath, AgentDirectoryName, _sessionId);
            _sessionFilePath = Path.Combine(_outputDirectory, SessionFileName);
            _actionsFilePath = Path.Combine(_outputDirectory, ActionsFileName);
            _startedAtText = DateTimeOffset.Now.ToString("o");
            _startedAtRealtime = Time.realtimeSinceStartupAsDouble;
            _result = "running";
            _message = string.Empty;

            Directory.CreateDirectory(_outputDirectory);
            File.WriteAllText(_actionsFilePath, string.Empty);
            EnsureDriver();
            InitializeForensics();
            SaveSessionReport();
        }

        /// <summary>
        /// 新しいセッションを開始し、LLM が参照する成果物ディレクトリを即作成します。
        /// </summary>
        public static AgentSession Begin(AgentGoal goal, AgentOptions options)
        {
            return new AgentSession(goal, options);
        }

        /// <summary>
        /// セッション識別子を返し、外部ログと DebugOutput の対応を固定します。
        /// </summary>
        public string SessionId
        {
            get { return _sessionId; }
        }

        /// <summary>
        /// セッション成果物のディレクトリを返し、外部運転手が追記や回収に使えるようにします。
        /// </summary>
        public string OutputDirectory
        {
            get { return _outputDirectory; }
        }

        /// <summary>
        /// 現在の観測を AI 向け圧縮テキストで返します。
        /// 差分だけを選べるようにして、長いプレイのトークン消費を抑えます。
        /// </summary>
        public string Observe(bool diffOnly)
        {
            var snapshot = UiSnapshot.Capture();
            var text = diffOnly && _lastSnapshot != null
                ? BuildDiffObservation(_lastSnapshot, snapshot)
                : BuildFullObservation(snapshot);
            _lastSnapshot = snapshot;
            return text;
        }

        /// <summary>
        /// 1 手を実行し、拒否理由や詰み判定を同じテキストで返します。
        /// 外部 LLM が次の判断へ失敗情報を戻せるようにするためです。
        /// </summary>
        public string Act(AgentAction action)
        {
            if (_ended)
            {
                return BuildStatusText("ended", "セッションは終了済みです。", Observe(false));
            }

            if (IsStepBudgetExceeded())
            {
                Finish("maxSteps", "手数上限に到達しました。");
                return BuildStatusText(_result, _message, Observe(false));
            }

            if (IsTimeBudgetExceeded())
            {
                Finish("maxSeconds", "実時間上限に到達しました。");
                return BuildStatusText(_result, _message, Observe(false));
            }

            var beforeSnapshot = UiSnapshot.Capture();
            var beforeObservationKey = UiSnapshot.ToCompactText(beforeSnapshot);
            var actionKind = GetActionKind(action);
            var target = GetActionTarget(action);
            var actionKey = BuildActionKey(action);
            if (IsForbidden(actionKey, target))
            {
                return RejectAction(action, beforeObservationKey, actionKind, target, "forbid に一致するため拒否しました。");
            }

            if (IsStuck(beforeObservationKey, actionKey))
            {
                Finish("stuck", "同じ観測または同じ観測で同じ行動が反復したため停止しました。");
                SaveAbnormalCapture("stuck");
                AppendActionLog(action, beforeObservationKey, actionKind, target, "stuck", _message, string.Empty);
                return BuildStatusText(_result, _message, BuildFullObservation(beforeSnapshot));
            }

            var forensicsStartCount = _forensics == null ? 0 : _forensics.CapturedCount;
            var message = string.Empty;
            try
            {
                message = ExecuteAction(action);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SaveAbnormalCapture("exception");
                message = $"例外発生: {exception.GetType().Name} {exception.Message}";
            }

            if (_forensics != null && _forensics.CapturedCount > forensicsStartCount)
            {
                message = $"{message} / 例外フォレンジックを保存しました。";
            }

            _scenarioSteps.Add(ToScenarioStep(action));
            var afterSnapshot = UiSnapshot.Capture();
            var diff = UiSnapshot.Compare(beforeSnapshot, afterSnapshot);
            var diffText = FormatDiff(diff);
            _lastSnapshot = afterSnapshot;
            AppendActionLog(action, beforeObservationKey, actionKind, target, "acted", message, diffText);

            if (IsGoalReached(afterSnapshot, diff))
            {
                Finish("reached", "目標を達成しました。");
            }

            SaveSessionReport();
            return BuildStatusText(_result, message, BuildFullObservation(afterSnapshot));
        }

        /// <summary>
        /// 目標条件を 02 の expect 語彙で評価し、LLM の自己申告を成功条件にしないようにします。
        /// </summary>
        public bool IsGoalReached()
        {
            var snapshot = UiSnapshot.Capture();
            return IsGoalReached(snapshot, null);
        }

        /// <summary>
        /// 成功した手順を 02 のシナリオ JSON として書き出し、探索結果を再実行可能なテストへ昇格します。
        /// </summary>
        public string ExportAsScenario(string name)
        {
            if (!IsGoalReached())
            {
                _message = "目標未達のため scenario.json は書き出しません。";
                SaveAbnormalCapture("goal-failed");
                SaveSessionReport();
                return string.Empty;
            }

            var steps = _scenarioSteps.ToArray();
            if (steps.Length > 0)
            {
                steps[steps.Length - 1].expect = _goal.goal ?? Array.Empty<ScenarioExpectation>();
            }

            var scenario = new UiScenario
            {
                name = string.IsNullOrEmpty(name) ? $"agent-{_sessionId}" : name,
                outputDirectory = _outputDirectory,
                stopOnFail = true,
                steps = steps,
            };

            _scenarioFilePath = Path.Combine(_outputDirectory, ScenarioFileName);
            File.WriteAllText(_scenarioFilePath, JsonUtility.ToJson(scenario, true));
            SaveSessionReport();
            return _scenarioFilePath;
        }

        /// <summary>
        /// セッションを明示終了し、外部運転手が成果物の確定時点を判断できるようにします。
        /// </summary>
        public void End()
        {
            if (_ended)
            {
                return;
            }

            Finish("ended", "外部から終了されました。");
        }

        /// <summary>
        /// ドライバと入力状態を破棄し、継続入力が次の検証へ残らないようにします。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
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
            _ownedForensics.Initialize(Path.Combine(_outputDirectory, "forensics"));
            _forensics = _ownedForensics;
        }

        private bool IsGoalReached(UiSnapshotDocument snapshot, UiSnapshotDiff diff)
        {
            return _evaluator.Evaluate(_goal.goal, snapshot, diff);
        }

        private string RejectAction(AgentAction action, string observationKey, string actionKind, string target, string message)
        {
            AppendActionLog(action, observationKey, actionKind, target, "rejected", message, string.Empty);
            SaveSessionReport();
            return BuildStatusText("rejected", message, BuildFullObservation(_lastSnapshot ?? UiSnapshot.Capture()));
        }

        private string ExecuteAction(AgentAction action)
        {
            if (action == null)
            {
                return "空の行動です。";
            }

            if (!string.IsNullOrEmpty(action.submit))
            {
                return ExecuteSubmit(action.submit);
            }

            if (!InputInjector.IsSupported && HasRawInputAction(action))
            {
                return "Input System が有効ではないため入力を省略しました。";
            }

#if ENABLE_INPUT_SYSTEM
            if (!string.IsNullOrEmpty(action.press))
            {
                if (TryParseGamepadButton(action.press, out var gamepadButton))
                {
                    InputInjector.Press(gamepadButton);
                    return "press を送信しました。";
                }

                return "未対応の gamepad button です。";
            }

            if (!string.IsNullOrEmpty(action.hold))
            {
                if (TryParseGamepadButton(action.hold, out var gamepadButton))
                {
                    EnsureDriver().Run(InputInjector.Hold(gamepadButton, ResolveSeconds(action.seconds)));
                    return "hold を開始しました。";
                }

                return "未対応の hold button です。";
            }

            if (!string.IsNullOrEmpty(action.key))
            {
                if (TryParseKey(action.key, out var key))
                {
                    InputInjector.Key(key);
                    return "key を送信しました。";
                }

                return "未対応の key です。";
            }
#endif

            if (!string.IsNullOrEmpty(action.move))
            {
                InputInjector.Move(ParseDirection(action.move));
                return "move を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.stick))
            {
                EnsureDriver().Run(InputInjector.Stick(action.stick, action.x, action.y, ResolveSeconds(action.seconds)));
                return "stick を開始しました。";
            }

            if (!string.IsNullOrEmpty(action.text))
            {
                EnsureDriver().Run(InputInjector.Text(action.text));
                return "text を開始しました。";
            }

            if (!string.IsNullOrEmpty(action.pointerMove))
            {
                InputInjector.PointerMove(ResolveScreenPosition(action.pointerMove, action.x, action.y));
                return "pointerMove を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.click))
            {
                InputInjector.Click(ResolveScreenPosition(action.click, action.x, action.y), ParsePointerButton(action.button));
                return "click を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.scroll))
            {
                InputInjector.Scroll(ResolveScreenPosition(action.scroll, action.x, action.y), action.amount);
                return "scroll を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.tap))
            {
                InputInjector.Tap(ResolveScreenPosition(action.tap, action.x, action.y));
                return "tap を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.drag))
            {
                var from = ResolveScreenPosition(action.from, action.fromX, action.fromY);
                var to = ResolveScreenPosition(action.to, action.toX, action.toY);
                EnsureDriver().Run(InputInjector.Drag(from, to, ResolveSeconds(action.seconds), ParsePointerButton(action.button)));
                return "drag を開始しました。";
            }

            if (!string.IsNullOrEmpty(action.swipe))
            {
                var from = ResolveScreenPosition(action.from, action.fromX, action.fromY);
                var to = ResolveScreenPosition(action.to, action.toX, action.toY);
                EnsureDriver().Run(InputInjector.Swipe(from, to, ResolveSeconds(action.seconds)));
                return "swipe を開始しました。";
            }

            if (!string.IsNullOrEmpty(action.pinch))
            {
                EnsureDriver().Run(InputInjector.Pinch(ResolveScreenPosition(action.center, action.x, action.y), action.fromDistance, action.toDistance, ResolveSeconds(action.seconds)));
                return "pinch を開始しました。";
            }

            return "解釈できる入力がありません。";
        }

        private string ExecuteSubmit(string targetName)
        {
            var target = UiInputLocator.FindByPathSegment(targetName);
            if (target == null)
            {
                return $"submit 対象が見つかりません。 target={targetName}";
            }

            var blockingObject = UiInputLocator.FindBlockingObject(target);
            if (blockingObject != null)
            {
                return $"submit 対象が遮られています。 target={targetName} blockedBy={blockingObject.name}";
            }

            if (!UiInputLocator.IsInteractable(target))
            {
                return $"submit 対象が操作可能ではありません。 target={targetName}";
            }

            return UiInputLocator.TrySubmit(target) ? "submit を送信しました。" : "submit を送れませんでした。";
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

        private string BuildFullObservation(UiSnapshotDocument snapshot)
        {
            _evaluator.Evaluate(_goal.goal, snapshot, null);
            var builder = new StringBuilder();
            builder.AppendLine(UiSnapshot.ToCompactText(snapshot));
            AppendCandidates(builder, snapshot);
            if (_driver != null && _driver.IsBusy)
            {
                builder.AppendLine("agent: inputBusy=true");
            }

            builder.Append("agent: settleFrames=");
            builder.AppendLine(ResolveSettleFrames().ToString(CultureInfo.InvariantCulture));

            AppendGoalFailures(builder);
            return builder.ToString().TrimEnd();
        }

        private string BuildDiffObservation(UiSnapshotDocument before, UiSnapshotDocument after)
        {
            var diff = UiSnapshot.Compare(before, after);
            _evaluator.Evaluate(_goal.goal, after, diff);
            var builder = new StringBuilder();
            builder.AppendLine(FormatDiff(diff));
            builder.AppendLine("game:");
            AppendGameState(builder, after);
            AppendCandidates(builder, after);
            if (_driver != null && _driver.IsBusy)
            {
                builder.AppendLine("agent: inputBusy=true");
            }

            builder.Append("agent: settleFrames=");
            builder.AppendLine(ResolveSettleFrames().ToString(CultureInfo.InvariantCulture));

            AppendGoalFailures(builder);
            return builder.ToString().TrimEnd();
        }

        private void AppendCandidates(StringBuilder builder, UiSnapshotDocument snapshot)
        {
            builder.AppendLine();
            builder.AppendLine("actions:");
            if (snapshot != null && snapshot.elements != null)
            {
                for (var elementIndex = 0; elementIndex < snapshot.elements.Length; elementIndex++)
                {
                    var element = snapshot.elements[elementIndex];
                    if (!IsActionCandidate(element))
                    {
                        continue;
                    }

                    var target = string.IsNullOrEmpty(element.path) ? element.name : element.path;
                    builder.Append(" - submit/click/tap target=");
                    builder.Append(target);
                    if (!string.IsNullOrEmpty(element.label))
                    {
                        builder.Append(" label=");
                        builder.Append(element.label);
                    }

                    builder.AppendLine();
                }
            }

            AppendRawInputCandidates(builder);
        }

        private void AppendRawInputCandidates(StringBuilder builder)
        {
            var inputMode = string.IsNullOrEmpty(_options.inputMode) ? "gamepad" : _options.inputMode;
            if (inputMode == "gamepad" || inputMode == "all")
            {
                builder.AppendLine(" - press=south/east/north/west/start/select/leftShoulder/rightShoulder");
                builder.AppendLine(" - move=up/down/left/right");
                builder.AppendLine(" - stick=left/right x=-1..1 y=-1..1 seconds=0.1");
            }

            if (inputMode == "keyboard" || inputMode == "all")
            {
                builder.AppendLine(" - key=Enter/Escape/Space/Tab/ArrowUp/ArrowDown/ArrowLeft/ArrowRight");
                builder.AppendLine(" - text=<文字列>");
            }

            if (inputMode == "mouse" || inputMode == "all")
            {
                builder.AppendLine(" - click=<target> button=left/right/middle");
                builder.AppendLine(" - scroll=<target> amount=<数値>");
            }

            if (inputMode == "touch" || inputMode == "all")
            {
                builder.AppendLine(" - tap=<target>");
                builder.AppendLine(" - swipe from=<target> to=<target> seconds=0.1");
                builder.AppendLine(" - pinch center=<target> fromDistance=<数値> toDistance=<数値>");
            }
        }

        private void AppendGameState(StringBuilder builder, UiSnapshotDocument snapshot)
        {
            if (snapshot == null || snapshot.game == null || snapshot.game.Length == 0)
            {
                builder.AppendLine(" -");
                return;
            }

            for (var gameIndex = 0; gameIndex < snapshot.game.Length; gameIndex++)
            {
                var entry = snapshot.game[gameIndex];
                if (entry == null)
                {
                    continue;
                }

                builder.Append(" ");
                builder.Append(entry.key);
                builder.Append("=");
                builder.Append(entry.value);
            }

            builder.AppendLine();
        }

        private void AppendGoalFailures(StringBuilder builder)
        {
            if (_evaluator.Failures.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine("goalFailures:");
            for (var failureIndex = 0; failureIndex < _evaluator.Failures.Count; failureIndex++)
            {
                var failure = _evaluator.Failures[failureIndex];
                builder.Append(" - ");
                builder.Append(failure.kind);
                builder.Append(" target=");
                builder.Append(failure.target);
                builder.Append(" value=");
                builder.Append(failure.value);
                builder.Append(" message=");
                builder.AppendLine(failure.message);
            }
        }

        private void AppendActionLog(AgentAction action, string observationKey, string actionKind, string target, string status, string message, string diffText)
        {
            var entry = new AgentActionLogEntry
            {
                step = _actionLogs.Count + 1,
                observationKey = observationKey ?? string.Empty,
                actionKind = actionKind ?? string.Empty,
                target = target ?? string.Empty,
                reason = string.IsNullOrEmpty(action == null ? string.Empty : action.reason) ? _options.defaultReason ?? string.Empty : action.reason,
                status = status ?? string.Empty,
                message = message ?? string.Empty,
                diff = diffText ?? string.Empty,
                createdAt = DateTimeOffset.Now.ToString("o"),
            };
            _actionLogs.Add(entry);
            File.AppendAllText(_actionsFilePath, JsonUtility.ToJson(entry, false) + Environment.NewLine);
        }

        private void SaveSessionReport()
        {
            var report = new AgentSessionReport
            {
                session = _sessionId,
                outputDirectory = _outputDirectory,
                result = _result,
                startedAt = _startedAtText,
                finishedAt = _ended ? DateTimeOffset.Now.ToString("o") : string.Empty,
                durationSeconds = (float)(Time.realtimeSinceStartupAsDouble - _startedAtRealtime),
                stepCount = _scenarioSteps.Count,
                maxSteps = ResolveMaxSteps(),
                maxSeconds = ResolveMaxSeconds(),
                goalReached = IsGoalReached(_lastSnapshot ?? UiSnapshot.Capture(), null),
                message = _message ?? string.Empty,
                scenario = _scenarioFilePath ?? string.Empty,
            };
            File.WriteAllText(_sessionFilePath, JsonUtility.ToJson(report, true));
        }

        private void SaveAbnormalCapture(string reason)
        {
            var fileName = $"{AbnormalCapturePrefix}{reason}-{DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}{CaptureFileExtension}";
            ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, fileName));
        }

        private void Finish(string result, string message)
        {
            _result = result;
            _message = message;
            _ended = true;
            SaveSessionReport();
        }

        private bool IsStepBudgetExceeded()
        {
            return _scenarioSteps.Count >= ResolveMaxSteps();
        }

        private bool IsTimeBudgetExceeded()
        {
            return Time.realtimeSinceStartupAsDouble - _startedAtRealtime >= ResolveMaxSeconds();
        }

        private int ResolveMaxSteps()
        {
            return _goal.maxSteps > 0 ? _goal.maxSteps : DefaultMaxSteps;
        }

        private int ResolveMaxSeconds()
        {
            return _goal.maxSeconds > 0 ? _goal.maxSeconds : DefaultMaxSeconds;
        }

        private int ResolveStuckRepeatLimit()
        {
            return _options.stuckRepeatLimit > 0 ? _options.stuckRepeatLimit : DefaultStuckRepeatLimit;
        }

        private static float ResolveSeconds(float seconds)
        {
            return seconds > 0.0f ? seconds : DefaultContinuousSeconds;
        }

        private bool IsStuck(string observationKey, string actionKey)
        {
            if (string.Equals(_lastRepeatedObservationKey, observationKey, StringComparison.Ordinal))
            {
                _sameObservationCount++;
            }
            else
            {
                _sameObservationCount = 1;
            }

            _lastRepeatedObservationKey = observationKey;
            if (string.Equals(_lastObservationKey, observationKey, StringComparison.Ordinal) && string.Equals(_lastActionKey, actionKey, StringComparison.Ordinal))
            {
                _sameObservationActionCount++;
            }
            else
            {
                _sameObservationActionCount = 1;
            }

            _lastObservationKey = observationKey;
            _lastActionKey = actionKey;
            return _sameObservationCount >= ResolveStuckRepeatLimit() || _sameObservationActionCount >= ResolveStuckRepeatLimit();
        }

        private bool IsForbidden(string actionKey, string target)
        {
            var forbiddenWords = _goal.forbid;
            if (forbiddenWords == null || forbiddenWords.Length == 0)
            {
                return false;
            }

            for (var forbiddenIndex = 0; forbiddenIndex < forbiddenWords.Length; forbiddenIndex++)
            {
                var forbiddenWord = forbiddenWords[forbiddenIndex];
                if (string.IsNullOrEmpty(forbiddenWord))
                {
                    continue;
                }

                if (ContainsOrdinalIgnoreCase(actionKey, forbiddenWord) || ContainsOrdinalIgnoreCase(target, forbiddenWord))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsOrdinalIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsActionCandidate(UiSnapshotElement element)
        {
            if (element == null)
            {
                return false;
            }

            if (!element.interactable || !string.IsNullOrEmpty(element.blockedBy))
            {
                return false;
            }

            return element.kind == "Button" || element.kind == "Toggle" || element.kind == "Input" || element.kind == "Selectable";
        }

        private static string FormatDiff(UiSnapshotDiff diff)
        {
            if (diff == null)
            {
                return "diff: -";
            }

            if (diff.isEmpty)
            {
                return "diff: empty";
            }

            var builder = new StringBuilder();
            builder.Append("diff:");
            AppendStringArray(builder, " added", diff.addedPaths);
            AppendStringArray(builder, " removed", diff.removedPaths);
            if (diff.changed != null)
            {
                for (var changeIndex = 0; changeIndex < diff.changed.Length; changeIndex++)
                {
                    var change = diff.changed[changeIndex];
                    if (change == null)
                    {
                        continue;
                    }

                    builder.Append(" changed=");
                    builder.Append(change.path);
                    builder.Append(".");
                    builder.Append(change.field);
                }
            }

            if (!string.Equals(diff.focusedBefore, diff.focusedAfter, StringComparison.Ordinal))
            {
                builder.Append(" focus=");
                builder.Append(diff.focusedBefore);
                builder.Append("->");
                builder.Append(diff.focusedAfter);
            }

            if (!string.Equals(diff.sceneBefore, diff.sceneAfter, StringComparison.Ordinal))
            {
                builder.Append(" scene=");
                builder.Append(diff.sceneBefore);
                builder.Append("->");
                builder.Append(diff.sceneAfter);
            }

            return builder.ToString();
        }

        private static void AppendStringArray(StringBuilder builder, string label, string[] values)
        {
            if (values == null)
            {
                return;
            }

            for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                builder.Append(label);
                builder.Append("=");
                builder.Append(values[valueIndex]);
            }
        }

        private static string BuildStatusText(string status, string message, string observation)
        {
            var builder = new StringBuilder();
            builder.Append("agent: status=");
            builder.Append(status);
            builder.Append(" message=");
            builder.AppendLine(message ?? string.Empty);
            builder.Append(observation ?? string.Empty);
            return builder.ToString().TrimEnd();
        }

        private UiScenarioStep ToScenarioStep(AgentAction action)
        {
            if (action == null)
            {
                return new UiScenarioStep();
            }

            return new UiScenarioStep
            {
                submit = action.submit,
                press = action.press,
                hold = action.hold,
                move = action.move,
                stick = action.stick,
                key = action.key,
                text = action.text,
                pointerMove = action.pointerMove,
                click = action.click,
                drag = action.drag,
                scroll = action.scroll,
                tap = action.tap,
                swipe = action.swipe,
                pinch = action.pinch,
                from = action.from,
                to = action.to,
                center = action.center,
                button = action.button,
                seconds = action.seconds,
                x = action.x,
                y = action.y,
                fromX = action.fromX,
                fromY = action.fromY,
                toX = action.toX,
                toY = action.toY,
                amount = action.amount,
                fromDistance = action.fromDistance,
                toDistance = action.toDistance,
                settleFrames = ResolveSettleFrames(),
            };
        }

        private int ResolveSettleFrames()
        {
            return _options.settleFrames > 0 ? _options.settleFrames : DefaultSettleFrames;
        }

        private static bool HasRawInputAction(AgentAction action)
        {
            return action != null && (!string.IsNullOrEmpty(action.press)
                || !string.IsNullOrEmpty(action.hold)
                || !string.IsNullOrEmpty(action.move)
                || !string.IsNullOrEmpty(action.stick)
                || !string.IsNullOrEmpty(action.key)
                || !string.IsNullOrEmpty(action.text)
                || !string.IsNullOrEmpty(action.pointerMove)
                || !string.IsNullOrEmpty(action.click)
                || !string.IsNullOrEmpty(action.drag)
                || !string.IsNullOrEmpty(action.scroll)
                || !string.IsNullOrEmpty(action.tap)
                || !string.IsNullOrEmpty(action.swipe)
                || !string.IsNullOrEmpty(action.pinch));
        }

        private static string GetActionKind(AgentAction action)
        {
            if (action == null) { return string.Empty; }
            if (!string.IsNullOrEmpty(action.submit)) { return "submit"; }
            if (!string.IsNullOrEmpty(action.press)) { return "press"; }
            if (!string.IsNullOrEmpty(action.hold)) { return "hold"; }
            if (!string.IsNullOrEmpty(action.move)) { return "move"; }
            if (!string.IsNullOrEmpty(action.stick)) { return "stick"; }
            if (!string.IsNullOrEmpty(action.key)) { return "key"; }
            if (!string.IsNullOrEmpty(action.text)) { return "text"; }
            if (!string.IsNullOrEmpty(action.pointerMove)) { return "pointerMove"; }
            if (!string.IsNullOrEmpty(action.click)) { return "click"; }
            if (!string.IsNullOrEmpty(action.drag)) { return "drag"; }
            if (!string.IsNullOrEmpty(action.scroll)) { return "scroll"; }
            if (!string.IsNullOrEmpty(action.tap)) { return "tap"; }
            if (!string.IsNullOrEmpty(action.swipe)) { return "swipe"; }
            if (!string.IsNullOrEmpty(action.pinch)) { return "pinch"; }
            return string.Empty;
        }

        private static string GetActionTarget(AgentAction action)
        {
            if (action == null) { return string.Empty; }
            if (!string.IsNullOrEmpty(action.submit)) { return action.submit; }
            if (!string.IsNullOrEmpty(action.pointerMove)) { return action.pointerMove; }
            if (!string.IsNullOrEmpty(action.click)) { return action.click; }
            if (!string.IsNullOrEmpty(action.scroll)) { return action.scroll; }
            if (!string.IsNullOrEmpty(action.tap)) { return action.tap; }
            if (!string.IsNullOrEmpty(action.from)) { return action.from; }
            if (!string.IsNullOrEmpty(action.to)) { return action.to; }
            if (!string.IsNullOrEmpty(action.center)) { return action.center; }
            if (!string.IsNullOrEmpty(action.press)) { return action.press; }
            if (!string.IsNullOrEmpty(action.hold)) { return action.hold; }
            if (!string.IsNullOrEmpty(action.move)) { return action.move; }
            if (!string.IsNullOrEmpty(action.stick)) { return action.stick; }
            if (!string.IsNullOrEmpty(action.key)) { return action.key; }
            return string.Empty;
        }

        private static string BuildActionKey(AgentAction action)
        {
            return action == null ? string.Empty : JsonUtility.ToJson(action, false);
        }

        private static Vector2 ResolveScreenPosition(string elementName, float fallbackX, float fallbackY)
        {
            if (!string.IsNullOrEmpty(elementName) && UiInputLocator.TryGetElementCenter(elementName, out var screenPosition))
            {
                return screenPosition;
            }

            return new Vector2(fallbackX, fallbackY);
        }

        private static FocusDirection ParseDirection(string direction)
        {
            switch (direction)
            {
                case "up": return FocusDirection.Up;
                case "down": return FocusDirection.Down;
                case "left": return FocusDirection.Left;
                case "right": return FocusDirection.Right;
                default: return FocusDirection.None;
            }
        }

        private static PointerButton ParsePointerButton(string button)
        {
            switch (button)
            {
                case "right": return PointerButton.Right;
                case "middle": return PointerButton.Middle;
                default: return PointerButton.Left;
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryParseGamepadButton(string value, out GamepadButton button)
        {
            switch (value)
            {
                case "south": button = GamepadButton.South; return true;
                case "east": button = GamepadButton.East; return true;
                case "north": button = GamepadButton.North; return true;
                case "west": button = GamepadButton.West; return true;
                case "dpadUp": button = GamepadButton.DpadUp; return true;
                case "dpadDown": button = GamepadButton.DpadDown; return true;
                case "dpadLeft": button = GamepadButton.DpadLeft; return true;
                case "dpadRight": button = GamepadButton.DpadRight; return true;
                case "leftShoulder": button = GamepadButton.LeftShoulder; return true;
                case "rightShoulder": button = GamepadButton.RightShoulder; return true;
                case "start": button = GamepadButton.Start; return true;
                case "select": button = GamepadButton.Select; return true;
                default: button = GamepadButton.South; return false;
            }
        }

        private static bool TryParseKey(string value, out Key key)
        {
            foreach (Key candidateKey in Enum.GetValues(typeof(Key)))
            {
                if (string.Equals(candidateKey.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    key = candidateKey;
                    return true;
                }
            }

            key = Key.None;
            return false;
        }
#endif
    }
}
#endif
