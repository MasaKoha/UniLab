#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace UniLab.AI
{
    /// <summary>
    /// JSON シナリオを UI 操作・入力語彙・期待値判定の単一入口へ統合し、結果をファイルで返します。
    /// </summary>
    public sealed class UiScenarioRunner : MonoBehaviour
    {
        private const string DefaultOutputDirectoryName = "ui-scenario", ScenarioResultsDirectoryName = "scenario-results", ReplayDirectoryName = "replays";
        private const string ResultVerdictPass = "pass", StepStatusPass = "pass", StepStatusFail = "fail";
        private const string FailureKindException = "exception", FailureKindStepResult = "stepResult", FailureKindTimeout = "timeout";
        private const string StepPhaseNone = "none", StepPhaseMonkey = "monkey", StepPhaseReady = "ready", StepPhaseBeforeSnapshot = "beforeSnapshot", StepPhaseAction = "action";
        private const string StepPhaseWaitScene = "waitScene", StepPhaseSettle = "settle", StepPhaseAfterSnapshot = "afterSnapshot", StepPhaseArtifacts = "artifacts";
        private const string StepPhaseExpectations = "expectations", StepPhaseStopRecording = "stopRecording", StepPhaseResult = "result";
        private const double StepTimeoutSeconds = 30.0;
        private const double StepTimeoutMultiplier = 2.0;
        private readonly List<ScenarioStepResult> _stepResults = new List<ScenarioStepResult>();
        private readonly List<ScenarioExpectationFailure> _currentFailures = new List<ScenarioExpectationFailure>();
        private readonly ScenarioInputExecutor _inputExecutor = new ScenarioInputExecutor();
        private readonly ScenarioExpectationEvaluator _expectationEvaluator = new ScenarioExpectationEvaluator();
        private UiScenario _scenario;
        private InputReplayer _inputReplayer;
        private ExceptionForensics _ownedForensics;
        private ExceptionForensics _forensics;
        private ScenarioArtifactWriter _artifactWriter;
        private ScenarioRecordingCoordinator _recordingCoordinator;
        private string _scenarioName;
        private string _outputDirectory;
        private string _resultFilePath;
        private string _currentStepPhase = StepPhaseNone;
        private int _warningCount;
        private double _startedAtRealtime;
        private string _startedAtText;
        private bool _completed;
        private bool _sessionStateEntered;
        /// <summary>
        /// シナリオ完了時に通知する。
        /// </summary>
        public event Action Completed;
        /// <summary>
        /// 完了時に結果 JSON のパスを渡す。ブリッジからの利用者はこのファイルをポーリングする。
        /// </summary>
        public event Action<string> ResultSaved;
        /// <summary>
        /// 新しい実行インスタンスを生成し、指定シナリオを開始する。
        /// </summary>
        public static UiScenarioRunner Run(UiScenario scenario)
        {
            return Run(scenario, string.Empty, string.Empty);
        }
        /// <summary>
        /// エディタメニューが結果ファイルの予定パスを先に返せるよう、出力先を外から固定して開始します。
        /// </summary>
        public static UiScenarioRunner Run(UiScenario scenario, string scenarioName, string resultFilePath)
        {
            var runnerObject = new GameObject(nameof(UiScenarioRunner));
            DontDestroyOnLoad(runnerObject);
            var runner = runnerObject.AddComponent<UiScenarioRunner>();
            runner.Initialize(scenario, scenarioName, resultFilePath);
            return runner;
        }
        /// <summary>
        /// メニューが Play 開始前に返す予定パスとランナー実保存先を一致させるための生成関数です。
        /// </summary>
        public static string CreateResultFilePath(string scenarioName)
        {
            var sanitizedScenarioName = UiScenarioStepReader.SanitizeFileName(scenarioName);
            var resultDirectory = Path.Combine(DebugOutputPath.DirectoryPath, ScenarioResultsDirectoryName, $"{sanitizedScenarioName}-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}");
            return Path.Combine(resultDirectory, $"{sanitizedScenarioName}.json");
        }
        private void Initialize(UiScenario scenario, string scenarioName, string resultFilePath)
        {
            _scenario = scenario ?? new UiScenario();
            _scenarioName = UiScenarioStepReader.ResolveScenarioName(_scenario, scenarioName);
            _outputDirectory = UiScenarioStepReader.ResolveOutputDirectory(_scenario, DefaultOutputDirectoryName);
            _resultFilePath = string.IsNullOrEmpty(resultFilePath) ? CreateResultFilePath(_scenarioName) : resultFilePath;
            _startedAtRealtime = Time.realtimeSinceStartupAsDouble;
            _startedAtText = DateTimeOffset.Now.ToString("o");
            _artifactWriter = new ScenarioArtifactWriter(_outputDirectory, _resultFilePath);
            _recordingCoordinator = new ScenarioRecordingCoordinator(_scenario, _scenarioName);
            Directory.CreateDirectory(_outputDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(_resultFilePath));
            AiSessionState.Enter("scenario");
            _sessionStateEntered = true;
            ForensicsContext.BeginScenario(_scenarioName);
            InitializeForensics();
            _recordingCoordinator.InitializeInputOverlay();
            _recordingCoordinator.InitializeInputRecording();
            _recordingCoordinator.InitializePerformanceRecording(IncrementWarningCount);
            StartCoroutine(RunScenarioCoroutine());
            UnityEngine.Debug.Log($"[UiScenarioRunner] 開始: scenario={_scenarioName} result={_resultFilePath}");
        }
        private void OnDestroy()
        {
            ExitSessionStateIfNeeded();
            _recordingCoordinator?.StopRecordingIfActive();
            _recordingCoordinator?.StopInputRecordingIfNeeded();
            _recordingCoordinator?.StopPerformanceRecordingIfNeeded();
            _recordingCoordinator?.HideScenarioOverlayIfNeeded();
            InputInjector.Dispose();
            _ownedForensics?.Dispose();
            ForensicsContext.Clear();
        }
        private IEnumerator RunScenarioCoroutine()
        {
            if (!string.IsNullOrEmpty(_scenario.replay))
            {
                yield return RunReplayCoroutine(_scenario.replay);
            }
            var steps = _scenario.steps ?? Array.Empty<UiScenarioStep>();
            for (var stepPosition = 0; stepPosition < steps.Length; stepPosition++)
            {
                var stepIndex = stepPosition + 1;
                yield return ExecuteStepWithTimeoutCoroutine(steps[stepPosition], stepIndex);
                if (_scenario.stopOnFail && _stepResults.Count > 0 && _stepResults[_stepResults.Count - 1].status == StepStatusFail)
                {
                    break;
                }
            }
            Finish(ResultVerdictPass);
        }
        private IEnumerator RunReplayCoroutine(string replayName)
        {
            var replayDirectory = UiScenarioStepReader.ResolveReplayDirectory(replayName, ReplayDirectoryName);
            var replayFinished = false;
            _inputReplayer = InputReplayer.StartReplay(replayDirectory);
            _inputReplayer.Completed += result =>
            {
                replayFinished = true;
                if (result.HasMismatch)
                {
                    _warningCount++;
                    UnityEngine.Debug.LogWarning($"[UiScenarioRunner] replay 不一致: {result.Message}");
                }
            };
            while (!replayFinished)
            {
                yield return null;
            }
            _inputReplayer = null;
        }
        private IEnumerator ExecuteStepWithTimeoutCoroutine(UiScenarioStep step, int stepIndex)
        {
            var stepResultCountBeforeStep = _stepResults.Count;
            var stepCompleted = false;
            Exception caughtException = null;
            var startedAt = Time.realtimeSinceStartupAsDouble;
            var timeoutSeconds = StepTimeoutSeconds * StepTimeoutMultiplier;
            var guardedCoroutine = StartCoroutine(GuardCoroutine(
                ExecuteStepCoroutine(step, stepIndex),
                exception => caughtException = exception,
                () => stepCompleted = true));
            while (!stepCompleted)
            {
                var elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
                if (elapsedSeconds > timeoutSeconds)
                {
                    StopCoroutine(guardedCoroutine);
                    _warningCount++;
                    AddFailure(FailureKindTimeout, UiScenarioStepReader.GetStepFailureTarget(step), string.Empty, $"ステップ全体がタイムアウトしました。 phase={_currentStepPhase} elapsed={elapsedSeconds:F2}s limit={timeoutSeconds:F2}s", string.Empty);
                    UnityEngine.Debug.LogWarning($"[UiScenarioRunner] ステップ全体がタイムアウトしました。 step={stepIndex} phase={_currentStepPhase} elapsed={elapsedSeconds:F2}s limit={timeoutSeconds:F2}s action={UiScenarioStepReader.CreateActionLabel(UiScenarioStepReader.EnsureStep(step))}");
                    CompleteInterruptedStep(step, stepIndex, elapsedSeconds, stepResultCountBeforeStep);
                    _currentStepPhase = StepPhaseNone;
                    yield break;
                }
                yield return null;
            }
            if (caughtException != null)
            {
                var elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
                AddFailure(FailureKindException, UiScenarioStepReader.GetStepFailureTarget(step), string.Empty, $"ステップ実行中に例外が発生しました。 phase={_currentStepPhase} {caughtException.GetType().Name}: {caughtException.Message}", string.Empty);
                UnityEngine.Debug.LogError($"[UiScenarioRunner] ステップ実行中に例外が発生しました。 step={stepIndex} phase={_currentStepPhase} action={UiScenarioStepReader.CreateActionLabel(UiScenarioStepReader.EnsureStep(step))}\n{caughtException}");
                CompleteInterruptedStep(step, stepIndex, elapsedSeconds, stepResultCountBeforeStep);
            }
            else if (_stepResults.Count == stepResultCountBeforeStep)
            {
                var elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
                _warningCount++;
                AddFailure(FailureKindStepResult, UiScenarioStepReader.GetStepFailureTarget(step), string.Empty, $"ステップ結果が追加されないまま coroutine が終了しました。 phase={_currentStepPhase}", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] ステップ結果が追加されないまま coroutine が終了しました。 step={stepIndex} phase={_currentStepPhase} action={UiScenarioStepReader.CreateActionLabel(UiScenarioStepReader.EnsureStep(step))}");
                CompleteInterruptedStep(step, stepIndex, elapsedSeconds, stepResultCountBeforeStep);
            }
            _currentStepPhase = StepPhaseNone;
        }
        private IEnumerator GuardCoroutine(IEnumerator rootCoroutine, Action<Exception> handleException, Action handleCompleted)
        {
            if (rootCoroutine == null)
            {
                handleCompleted?.Invoke();
                yield break;
            }
            var coroutineStack = new Stack<IEnumerator>();
            coroutineStack.Push(rootCoroutine);
            while (coroutineStack.Count > 0)
            {
                var currentCoroutine = coroutineStack.Peek();
                object yieldedObject;
                bool hasNext;
                try
                {
                    hasNext = currentCoroutine.MoveNext();
                    yieldedObject = hasNext ? currentCoroutine.Current : null;
                }
                catch (Exception exception)
                {
                    handleException?.Invoke(exception);
                    handleCompleted?.Invoke();
                    yield break;
                }
                if (!hasNext)
                {
                    coroutineStack.Pop();
                    continue;
                }
                if (yieldedObject is IEnumerator nestedCoroutine)
                {
                    coroutineStack.Push(nestedCoroutine);
                    continue;
                }
                yield return yieldedObject;
            }
            handleCompleted?.Invoke();
        }
        private IEnumerator ExecuteStepCoroutine(UiScenarioStep step, int stepIndex)
        {
            step = UiScenarioStepReader.EnsureStep(step);
            _currentFailures.Clear();
            var forensicsStartCount = _forensics == null ? 0 : _forensics.CapturedCount;
            var actionLabel = UiScenarioStepReader.CreateActionLabel(step);
            ForensicsContext.SetStep(stepIndex, actionLabel);
            _recordingCoordinator.MarkPerformanceStep(stepIndex, actionLabel);
            _recordingCoordinator.BeginRecordingIfNeeded(step, IncrementWarningCount);
            if (step.monkey != null)
            {
                _currentStepPhase = StepPhaseMonkey;
                yield return RunMonkeyStepCoroutine(step, stepIndex);
                yield break;
            }
            var waitedSeconds = 0.0f;
            var waitFailure = string.Empty;
            _currentStepPhase = StepPhaseReady;
            yield return WaitForReadyCoroutine(step, stepIndex, value => waitedSeconds = value, value => waitFailure = value);
            if (!string.IsNullOrEmpty(waitFailure))
            {
                AddFailure("ready", UiScenarioStepReader.GetPrimaryTarget(step), string.Empty, waitFailure, string.Empty);
            }
            _currentStepPhase = StepPhaseBeforeSnapshot;
            var beforeSnapshot = UiSnapshot.Capture();
            _recordingCoordinator.InputRecorder?.SetAnchor(UiScenarioStepReader.CreateAnchor(step));
            if (string.IsNullOrEmpty(waitFailure))
            {
                _currentStepPhase = StepPhaseAction;
                yield return ExecuteActionCoroutine(step, stepIndex);
                _currentStepPhase = StepPhaseWaitScene;
                yield return WaitSceneAfterActionCoroutine(step, stepIndex);
            }
            var settleFrames = UiScenarioStepReader.GetSettleFrameCount(step);
            _currentStepPhase = StepPhaseSettle;
            for (var frame = 0; frame < settleFrames; frame++)
            {
                yield return null;
            }
            _currentStepPhase = StepPhaseAfterSnapshot;
            var afterSnapshot = UiSnapshot.Capture();
            var diff = UiSnapshot.Compare(beforeSnapshot, afterSnapshot);
            if (ScenarioInputExecutor.IsInputStep(step) && diff.isEmpty)
            {
                _warningCount++;
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 入力前後のスナップショット差分が空でした。 step={stepIndex}");
            }
            _currentStepPhase = StepPhaseArtifacts;
            yield return _artifactWriter.SaveStepArtifactsCoroutine(step, afterSnapshot, stepIndex, AddFailure, IncrementWarningCount, () => { }, () => { });
            _currentStepPhase = StepPhaseExpectations;
            AddExpectationFailures(step, afterSnapshot, diff, forensicsStartCount);
            _currentStepPhase = StepPhaseStopRecording;
            _recordingCoordinator.StopRecordingIfNeeded(step);
            _currentStepPhase = StepPhaseResult;
            AddStepResult(step, stepIndex, waitedSeconds, afterSnapshot);
        }
        private IEnumerator RunMonkeyStepCoroutine(UiScenarioStep step, int stepIndex)
        {
            var completed = false;
            MonkeySummary summary = null;
            var tester = MonkeyTester.Start(step.monkey);
            tester.Completed += result =>
            {
                summary = result;
                completed = true;
            };
            while (!completed)
            {
                yield return null;
            }
            if (summary != null && summary.violationCount > 0)
            {
                AddFailure("monkey", string.Empty, summary.outputDirectory, $"モンキーテスターで違反を検出しました。 count={summary.violationCount}", summary.outputDirectory);
            }
            var snapshot = UiSnapshot.Capture();
            AddStepResult(step, stepIndex, 0.0f, snapshot);
        }
        private IEnumerator WaitForReadyCoroutine(UiScenarioStep step, int stepIndex, Action<float> setWaitedSeconds, Action<string> setFailure)
        {
            var startedAt = Time.realtimeSinceStartupAsDouble;
            while (!IsReady(step, out var failureMessage))
            {
                if (Time.realtimeSinceStartupAsDouble - startedAt > StepTimeoutSeconds)
                {
                    _warningCount++;
                    setWaitedSeconds((float)(Time.realtimeSinceStartupAsDouble - startedAt));
                    setFailure(failureMessage);
                    UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 準備待ちがタイムアウトしました。 step={stepIndex} reason={failureMessage}");
                    yield break;
                }
                yield return null;
            }
            setWaitedSeconds((float)(Time.realtimeSinceStartupAsDouble - startedAt));
        }
        private IEnumerator ExecuteActionCoroutine(UiScenarioStep step, int stepIndex)
        {
            var actionLabel = UiScenarioStepReader.CreateActionLabel(step);
            ForensicsContext.SetStep(stepIndex, actionLabel);
            _recordingCoordinator.UpdateRecordingContext();
            _recordingCoordinator.AddRecordingMarkerIfNeeded(actionLabel);
            if (!string.IsNullOrEmpty(step.submit))
            {
                InputOverlay.SetStepLabel($"決定 [{step.submit}]");
                var target = UiInputLocator.FindTarget(step.submit);
                if (target == null || !UiInputLocator.TrySubmit(target))
                {
                    _warningCount++;
                    AddFailure("submit", step.submit, string.Empty, "submit を送れませんでした。", string.Empty);
                }
                yield break;
            }
            yield return _inputExecutor.ExecuteInputCoroutine(step, AddFailure);
        }
        private IEnumerator WaitSceneAfterActionCoroutine(UiScenarioStep step, int stepIndex)
        {
            if (string.IsNullOrEmpty(step.waitScene))
            {
                yield break;
            }
            var startedAt = Time.realtimeSinceStartupAsDouble;
            while (!UiInputLocator.IsSceneLoaded(step.waitScene))
            {
                if (Time.realtimeSinceStartupAsDouble - startedAt > StepTimeoutSeconds)
                {
                    _warningCount++;
                    AddFailure("waitScene", step.waitScene, string.Empty, "操作後のシーン待ちがタイムアウトしました。", string.Empty);
                    UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 操作後のシーン待ちがタイムアウトしました。 step={stepIndex} scene={step.waitScene}");
                    yield break;
                }
                yield return null;
            }
        }
        private bool IsReady(UiScenarioStep step, out string failureMessage)
        {
            var anchor = UiScenarioStepReader.CreateAnchor(step);
            if (!UiInputLocator.IsAnchorSatisfied(anchor))
            {
                failureMessage = "待機条件が満たされませんでした。";
                return false;
            }
            var primaryTarget = UiScenarioStepReader.GetPrimaryTarget(step);
            if (string.IsNullOrEmpty(primaryTarget))
            {
                failureMessage = string.Empty;
                return true;
            }
            if (!string.IsNullOrEmpty(step.scrollTo) && string.IsNullOrEmpty(step.submit))
            {
                return UiReadiness.Exists(primaryTarget, out failureMessage);
            }
            return UiReadiness.IsSubmittable(primaryTarget, out failureMessage);
        }
        private void AddFailure(string kind, string target, string value, string message, string evidencePath)
        {
            _currentFailures.Add(new ScenarioExpectationFailure
            {
                kind = kind ?? string.Empty,
                target = target ?? string.Empty,
                value = value ?? string.Empty,
                message = message ?? string.Empty,
                evidencePath = evidencePath ?? string.Empty,
            });
        }
        private void AddStepResult(UiScenarioStep step, int stepIndex, float waitedSeconds, UiSnapshotDocument snapshot)
        {
            var failed = _currentFailures.Count > 0;
            var evidence = failed ? _artifactWriter.SaveFailureEvidenceSafely(stepIndex, snapshot, AddFailure, IncrementWarningCount) : new ScenarioStepEvidence();
            _stepResults.Add(new ScenarioStepResult
            {
                index = stepIndex,
                submit = step.submit ?? string.Empty,
                input = ScenarioInputExecutor.GetInputKind(step),
                status = failed ? StepStatusFail : StepStatusPass,
                waitedSeconds = waitedSeconds,
                failures = _currentFailures.ToArray(),
                evidence = evidence,
            });
        }
        private void CompleteInterruptedStep(UiScenarioStep step, int stepIndex, double elapsedSeconds, int stepResultCountBeforeStep)
        {
            var ensuredStep = UiScenarioStepReader.EnsureStep(step);
            try
            {
                _recordingCoordinator.StopRecordingIfNeeded(ensuredStep);
            }
            catch (Exception exception)
            {
                _warningCount++;
                AddFailure(FailureKindException, UiScenarioStepReader.GetStepFailureTarget(ensuredStep), string.Empty, $"中断ステップの録画停止に失敗しました。 {exception.GetType().Name}: {exception.Message}", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 中断ステップの録画停止に失敗しました。 step={stepIndex} {exception.GetType().Name}: {exception.Message}");
            }
            if (_stepResults.Count > stepResultCountBeforeStep)
            {
                return;
            }
            var snapshot = CaptureSnapshotForInterruptedStep(stepIndex);
            AddStepResult(ensuredStep, stepIndex, (float)elapsedSeconds, snapshot);
        }
        private void Finish(string requestedVerdict)
        {
            if (_completed)
            {
                return;
            }
            _completed = true;
            ExitSessionStateIfNeeded();
            _recordingCoordinator.StopRecordingIfActive();
            _recordingCoordinator.StopInputRecordingIfNeeded();
            _recordingCoordinator.StopPerformanceRecordingIfNeeded();
            _recordingCoordinator.HideScenarioOverlayIfNeeded();
            _artifactWriter.SaveResult(requestedVerdict, _scenario, _scenarioName, _startedAtText, _startedAtRealtime, _stepResults, _recordingCoordinator.RecordingDirectories, _forensics, _warningCount, _recordingCoordinator.DroppedFrameCount, _recordingCoordinator.PerformanceReportPath);
            ResultSaved?.Invoke(_resultFilePath);
            Completed?.Invoke();
            Destroy(gameObject);
        }
        private void InitializeForensics()
        {
            _forensics = ExceptionForensics.Current;
            if (_forensics != null)
            {
                return;
            }
            _ownedForensics = new ExceptionForensics();
            _ownedForensics.Initialize();
            _forensics = _ownedForensics;
        }
        private void AddExpectationFailures(UiScenarioStep step, UiSnapshotDocument snapshot, UiSnapshotDiff diff, int forensicsStartCount)
        {
            var failures = _expectationEvaluator.EvaluateExpectations(step, snapshot, diff, forensicsStartCount, _forensics, _recordingCoordinator.VideoRecorder, _recordingCoordinator.PerformanceRecorder);
            for (var failureIndex = 0; failureIndex < failures.Count; failureIndex++)
            {
                var failure = failures[failureIndex];
                AddFailure(failure.kind, failure.target, failure.value, failure.message, failure.evidencePath);
            }
        }
        private UiSnapshotDocument CaptureSnapshotForInterruptedStep(int stepIndex)
        {
            try
            {
                return UiSnapshot.Capture();
            }
            catch (Exception exception)
            {
                _warningCount++;
                AddFailure(FailureKindException, $"step{stepIndex}", string.Empty, $"中断ステップのスナップショット取得に失敗しました。 {exception.GetType().Name}: {exception.Message}", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 中断ステップのスナップショット取得に失敗しました。 step={stepIndex} {exception.GetType().Name}: {exception.Message}");
                return UiScenarioStepReader.CreateFallbackSnapshot();
            }
        }
        private void IncrementWarningCount()
        {
            _warningCount++;
        }

        private void ExitSessionStateIfNeeded()
        {
            if (!_sessionStateEntered)
            {
                return;
            }

            _sessionStateEntered = false;
            AiSessionState.Exit("scenario");
        }
    }
}
#endif
