#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>行動履歴と成果物の保存形式をセッション制御から分離します。</summary>
    internal sealed class AgentSessionArtifacts
    {
        private const string AgentDirectoryName = "agent";
        private const string SessionFileName = "session.json";
        private const string ActionsFileName = "actions.jsonl";
        private const string ScenarioFileName = "scenario.json";
        private const string AbnormalCapturePrefix = "abnormal-";
        private const string CaptureFileExtension = ".png";
        private const string SessionTimestampFormat = "yyyyMMdd-HHmmss";

        private readonly AgentGoal _goal;
        private readonly AgentOptions _options;
        private readonly AgentSessionGuards _guards;
        private readonly AgentExpectationEvaluator _evaluator;
        private readonly List<AgentActionLogEntry> _actionLogs = new List<AgentActionLogEntry>();
        private readonly List<UiScenarioStep> _scenarioSteps = new List<UiScenarioStep>();
        private readonly string _sessionId;
        private readonly string _outputDirectory;
        private readonly string _sessionFilePath;
        private readonly string _actionsFilePath;
        private readonly string _startedAtText;
        private readonly double _startedAtRealtime;
        private string _scenarioFilePath;

        /// <summary>成果物の識別子と開始時刻を揃えるために生成時に固定します。</summary>
        internal AgentSessionArtifacts(AgentGoal goal, AgentOptions options, AgentSessionGuards guards, AgentExpectationEvaluator evaluator)
        {
            _goal = goal;
            _options = options;
            _guards = guards;
            _evaluator = evaluator;
            _sessionId = DateTime.Now.ToString(SessionTimestampFormat, CultureInfo.InvariantCulture);
            _outputDirectory = Path.Combine(DebugOutputPath.DirectoryPath, AgentDirectoryName, _sessionId);
            _sessionFilePath = Path.Combine(_outputDirectory, SessionFileName);
            _actionsFilePath = Path.Combine(_outputDirectory, ActionsFileName);
            _startedAtText = DateTimeOffset.Now.ToString("o");
            _startedAtRealtime = Time.realtimeSinceStartupAsDouble;
        }

        /// <summary>外部ログと成果物を対応付ける識別子です。</summary>
        internal string SessionId => _sessionId;

        /// <summary>外部運転手が成果物を回収するためのディレクトリです。</summary>
        internal string OutputDirectory => _outputDirectory;

        /// <summary>予算判定とレポートで共通の開始時刻です。</summary>
        internal double StartedAtRealtime => _startedAtRealtime;

        /// <summary>拒否された行動を手数へ含めないための実行ステップ数です。</summary>
        internal int StepCount => _scenarioSteps.Count;

        /// <summary>フォレンジックの保存先をセッション成果物の配下に固定します。</summary>
        internal string ForensicsDirectory => Path.Combine(_outputDirectory, "forensics");

        /// <summary>セッション開始直後から外部で成果物を参照できるようにします。</summary>
        internal void Initialize()
        {
            Directory.CreateDirectory(_outputDirectory);
            File.WriteAllText(_actionsFilePath, string.Empty);
        }

        /// <summary>実行済みの行動を再生可能なステップとして保持します。</summary>
        internal void RecordScenarioStep(AgentAction action)
        {
            _scenarioSteps.Add(ToScenarioStep(action));
        }

        /// <summary>拒否や停止も含めた行動を追跡可能な形式で追記します。</summary>
        internal void AppendActionLog(AgentAction action, string observationKey, string actionKind, string target, string status, string message, string diffText)
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

        /// <summary>現在の結果と目標判定を成果物へ確定します。</summary>
        internal void SaveSessionReport(string result, bool ended, string message, UiSnapshotDocument lastSnapshot)
        {
            var report = new AgentSessionReport
            {
                session = _sessionId,
                outputDirectory = _outputDirectory,
                result = result,
                startedAt = _startedAtText,
                finishedAt = ended ? DateTimeOffset.Now.ToString("o") : string.Empty,
                durationSeconds = (float)(Time.realtimeSinceStartupAsDouble - _startedAtRealtime),
                stepCount = _scenarioSteps.Count,
                maxSteps = _guards.ResolveMaxSteps(),
                maxSeconds = _guards.ResolveMaxSeconds(),
                goalReached = !_goal.freePlay && _evaluator.Evaluate(_goal.goal, lastSnapshot ?? UiSnapshot.Capture(), null),
                message = message ?? string.Empty,
                scenario = _scenarioFilePath ?? string.Empty,
            };
            File.WriteAllText(_sessionFilePath, JsonUtility.ToJson(report, true));
        }

        /// <summary>停止や例外の原因を後から確認できる画像を残します。</summary>
        internal void SaveAbnormalCapture(string reason)
        {
            var fileName = $"{AbnormalCapturePrefix}{reason}-{DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}{CaptureFileExtension}";
            ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, fileName));
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
                scrollTo = action.scrollTo,
                expect = action.expect,
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
                settleFrames = AgentObservationFormatter.ResolveSettleFrames(_options),
            };
        }

        /// <summary>達成済みの行動列をシナリオとして書き出します。</summary>
        internal string ExportAsScenario(string name)
        {
            var steps = _scenarioSteps.ToArray();
            if (!_goal.freePlay && steps.Length > 0)
            {
                var finalStep = JsonUtility.FromJson<UiScenarioStep>(JsonUtility.ToJson(steps[steps.Length - 1]));
                var expectations = new List<ScenarioExpectation>(finalStep.expect ?? Array.Empty<ScenarioExpectation>());
                expectations.AddRange(_goal.goal ?? Array.Empty<ScenarioExpectation>());
                finalStep.expect = expectations.ToArray();
                steps[steps.Length - 1] = finalStep;
                // 目標がシーン到達なら、最終ステップの操作後にそのシーンを待ってから expect を評価させる。
                // 待ちが無いと遷移フェード中（前のシーン）に評価されて必ず失敗する（2026-09-02 再生で実測）
                var sceneGoal = Array.Find(steps[steps.Length - 1].expect, expectation => expectation != null && expectation.kind == "sceneIs");
                if (sceneGoal != null && string.IsNullOrEmpty(steps[steps.Length - 1].waitScene))
                {
                    steps[steps.Length - 1].waitScene = sceneGoal.value;
                }
            }

            var scenario = new UiScenario
            {
                name = string.IsNullOrEmpty(name) ? $"agent-{_sessionId}" : name,
                outputDirectory = _outputDirectory,
                stopOnFail = true,
                steps = steps,
            };

            _scenarioFilePath = Path.Combine(_outputDirectory, ScenarioFileName);
            File.WriteAllText(_scenarioFilePath, UiScenarioJsonPresence.StripDefaultMonkey(JsonUtility.ToJson(scenario, true)));
            return _scenarioFilePath;
        }
    }
}
#endif
