#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace UniLab.AI
{
    /// <summary>
    /// JSON シナリオを UI 操作・入力語彙・期待値判定の単一入口へ統合し、結果をファイルで返します。
    /// </summary>
    public sealed class UiScenarioRunner : MonoBehaviour
    {
        private const string DefaultOutputDirectoryName = "ui-scenario";
        private const string ScenarioResultsDirectoryName = "scenario-results";
        private const string ReplayDirectoryName = "replays";
        private const string RecordingDirectoryName = "recordings";
        private const string CurrentRecordingDirectoryName = "_current";
        private const string TemporaryRecordingName = "recording";
        private const string CaptureFileExtension = ".png";
        private const string JsonFileExtension = ".json";
        private const string AuditFileNameSuffix = "-audit.json";
        private const string ResultVerdictPass = "pass";
        private const string ResultVerdictFail = "fail";
        private const string ResultVerdictError = "error";
        private const string StepStatusPass = "pass";
        private const string StepStatusFail = "fail";
        private const string FailureKindCapture = "capture";
        private const string FailureKindEvidence = "evidence";
        private const string FailureKindException = "exception";
        private const string FailureKindStepResult = "stepResult";
        private const string FailureKindTimeout = "timeout";
        private const string StepPhaseNone = "none";
        private const string StepPhaseMonkey = "monkey";
        private const string StepPhaseReady = "ready";
        private const string StepPhaseBeforeSnapshot = "beforeSnapshot";
        private const string StepPhaseAction = "action";
        private const string StepPhaseWaitScene = "waitScene";
        private const string StepPhaseSettle = "settle";
        private const string StepPhaseAfterSnapshot = "afterSnapshot";
        private const string StepPhaseArtifacts = "artifacts";
        private const string StepPhaseExpectations = "expectations";
        private const string StepPhaseStopRecording = "stopRecording";
        private const string StepPhaseResult = "result";
        private const int DefaultSettleFrames = 30;
        private const int RecordingFramesPerSecond = 30;
        private const double StepTimeoutSeconds = 30.0;
        private const double StepTimeoutMultiplier = 2.0;

        private readonly List<ScenarioStepResult> _stepResults = new List<ScenarioStepResult>();
        private readonly List<string> _recordingDirectories = new List<string>();
        private readonly List<ScenarioExpectationFailure> _currentFailures = new List<ScenarioExpectationFailure>();

        private UiScenario _scenario;
        private VideoRecorder _videoRecorder;
        private InputRecorder _inputRecorder;
        private InputReplayer _inputReplayer;
        private PerformanceRecorder _performanceRecorder;
        private ExceptionForensics _ownedForensics;
        private ExceptionForensics _forensics;
        private string _scenarioName;
        private string _outputDirectory;
        private string _resultFilePath;
        private string _performanceReportPath;
        private string _currentStepPhase = StepPhaseNone;
        private int _warningCount;
        private int _auditCount;
        private int _captureCount;
        private int _recordingCount;
        private int _droppedFrameCount;
        private double _startedAtRealtime;
        private string _startedAtText;
        private bool _completed;
        private bool _showedScenarioOverlay;

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
            var resultDirectory = Path.Combine(DebugOutputPath.DirectoryPath, ScenarioResultsDirectoryName, $"{SanitizeFileName(scenarioName)}-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}");
            return Path.Combine(resultDirectory, $"{SanitizeFileName(scenarioName)}.json");
        }

        private void Initialize(UiScenario scenario, string scenarioName, string resultFilePath)
        {
            _scenario = scenario ?? new UiScenario();
            _scenarioName = ResolveScenarioName(_scenario, scenarioName);
            _outputDirectory = ResolveOutputDirectory(_scenario);
            _resultFilePath = string.IsNullOrEmpty(resultFilePath) ? CreateResultFilePath(_scenarioName) : resultFilePath;
            _startedAtRealtime = Time.realtimeSinceStartupAsDouble;
            _startedAtText = DateTimeOffset.Now.ToString("o");

            Directory.CreateDirectory(_outputDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(_resultFilePath));
            ForensicsContext.BeginScenario(_scenarioName);
            InitializeForensics();
            InitializeInputOverlay();
            InitializeInputRecording();
            InitializePerformanceRecording();

            StartCoroutine(RunScenarioCoroutine());
            UnityEngine.Debug.Log($"[UiScenarioRunner] 開始: scenario={_scenarioName} result={_resultFilePath}");
        }

        private void OnDestroy()
        {
            StopRecordingIfActive();
            StopInputRecordingIfNeeded();
            StopPerformanceRecordingIfNeeded();
            HideScenarioOverlayIfNeeded();
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
            var replayDirectory = ResolveReplayDirectory(replayName);
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
                    AddFailure(FailureKindTimeout, GetStepFailureTarget(step), string.Empty, $"ステップ全体がタイムアウトしました。 phase={_currentStepPhase} elapsed={elapsedSeconds:F2}s limit={timeoutSeconds:F2}s", string.Empty);
                    UnityEngine.Debug.LogWarning($"[UiScenarioRunner] ステップ全体がタイムアウトしました。 step={stepIndex} phase={_currentStepPhase} elapsed={elapsedSeconds:F2}s limit={timeoutSeconds:F2}s action={CreateActionLabel(EnsureStep(step))}");
                    CompleteInterruptedStep(step, stepIndex, elapsedSeconds, stepResultCountBeforeStep);
                    _currentStepPhase = StepPhaseNone;
                    yield break;
                }

                yield return null;
            }

            if (caughtException != null)
            {
                var elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
                AddFailure(FailureKindException, GetStepFailureTarget(step), string.Empty, $"ステップ実行中に例外が発生しました。 phase={_currentStepPhase} {caughtException.GetType().Name}: {caughtException.Message}", string.Empty);
                UnityEngine.Debug.LogError($"[UiScenarioRunner] ステップ実行中に例外が発生しました。 step={stepIndex} phase={_currentStepPhase} action={CreateActionLabel(EnsureStep(step))}\n{caughtException}");
                CompleteInterruptedStep(step, stepIndex, elapsedSeconds, stepResultCountBeforeStep);
            }
            else if (_stepResults.Count == stepResultCountBeforeStep)
            {
                var elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
                _warningCount++;
                AddFailure(FailureKindStepResult, GetStepFailureTarget(step), string.Empty, $"ステップ結果が追加されないまま coroutine が終了しました。 phase={_currentStepPhase}", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] ステップ結果が追加されないまま coroutine が終了しました。 step={stepIndex} phase={_currentStepPhase} action={CreateActionLabel(EnsureStep(step))}");
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
            step = EnsureStep(step);
            _currentFailures.Clear();
            var forensicsStartCount = _forensics == null ? 0 : _forensics.CapturedCount;
            var actionLabel = CreateActionLabel(step);
            ForensicsContext.SetStep(stepIndex, actionLabel);
            MarkPerformanceStep(stepIndex, actionLabel);
            BeginRecordingIfNeeded(step);

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
                AddFailure("ready", GetPrimaryTarget(step), string.Empty, waitFailure, string.Empty);
            }

            _currentStepPhase = StepPhaseBeforeSnapshot;
            var beforeSnapshot = UiSnapshot.Capture();
            _inputRecorder?.SetAnchor(CreateAnchor(step));
            if (string.IsNullOrEmpty(waitFailure))
            {
                _currentStepPhase = StepPhaseAction;
                yield return ExecuteActionCoroutine(step, stepIndex);
                _currentStepPhase = StepPhaseWaitScene;
                yield return WaitSceneAfterActionCoroutine(step, stepIndex);
            }
            var settleFrames = GetSettleFrameCount(step);
            _currentStepPhase = StepPhaseSettle;
            for (var frame = 0; frame < settleFrames; frame++)
            {
                yield return null;
            }

            _currentStepPhase = StepPhaseAfterSnapshot;
            var afterSnapshot = UiSnapshot.Capture();
            var diff = UiSnapshot.Compare(beforeSnapshot, afterSnapshot);
            if (IsInputStep(step) && diff.isEmpty)
            {
                _warningCount++;
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 入力前後のスナップショット差分が空でした。 step={stepIndex}");
            }

            _currentStepPhase = StepPhaseArtifacts;
            yield return SaveStepArtifactsCoroutine(step, afterSnapshot, stepIndex);
            _currentStepPhase = StepPhaseExpectations;
            EvaluateExpectations(step, afterSnapshot, diff, forensicsStartCount);
            _currentStepPhase = StepPhaseStopRecording;
            StopRecordingIfNeeded(step);
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
            var actionLabel = CreateActionLabel(step);
            ForensicsContext.SetStep(stepIndex, actionLabel);
            UpdateRecordingContext();
            AddRecordingMarkerIfNeeded(actionLabel);

            if (!string.IsNullOrEmpty(step.submit))
            {
                var target = UiInputLocator.FindByPathSegment(step.submit);
                if (target == null || !UiInputLocator.TrySubmit(target))
                {
                    _warningCount++;
                    AddFailure("submit", step.submit, string.Empty, "submit を送れませんでした。", string.Empty);
                }

                yield break;
            }

            yield return ExecuteInputCoroutine(step);
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

        private IEnumerator ExecuteInputCoroutine(UiScenarioStep step)
        {
            if (!InputInjector.IsSupported && IsInputStep(step))
            {
                AddFailure("input", GetInputKind(step), string.Empty, "Input System が有効ではありません。", string.Empty);
                yield break;
            }

#if ENABLE_INPUT_SYSTEM
            if (!string.IsNullOrEmpty(step.press))
            {
                if (TryParseGamepadButton(step.press, out var button))
                {
                    InputInjector.Press(button);
                }
                else
                {
                    AddFailure("press", string.Empty, step.press, "未対応の gamepad button です。", string.Empty);
                }

                yield break;
            }

            if (!string.IsNullOrEmpty(step.hold))
            {
                if (TryParseGamepadButton(step.hold, out var button))
                {
                    yield return InputInjector.Hold(button, step.seconds);
                }
                else
                {
                    AddFailure("hold", string.Empty, step.hold, "未対応の hold button です。", string.Empty);
                }

                yield break;
            }

            if (!string.IsNullOrEmpty(step.key))
            {
                if (TryParseKey(step.key, out var key))
                {
                    InputInjector.Key(key);
                }
                else
                {
                    AddFailure("key", string.Empty, step.key, "未対応の key です。", string.Empty);
                }

                yield break;
            }
#endif

            if (!string.IsNullOrEmpty(step.move))
            {
                InputInjector.Move(ParseDirection(step.move));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.stick))
            {
                yield return InputInjector.Stick(step.stick, step.x, step.y, step.seconds);
                yield break;
            }

            if (!string.IsNullOrEmpty(step.text))
            {
                yield return InputInjector.Text(step.text);
                yield break;
            }

            if (!string.IsNullOrEmpty(step.pointerMove))
            {
                InputInjector.PointerMove(ResolveScreenPosition(step.pointerMove, step.x, step.y));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.click))
            {
                InputInjector.Click(ResolveScreenPosition(step.click, step.x, step.y), ParsePointerButton(step.button));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.scroll))
            {
                InputInjector.Scroll(ResolveScreenPosition(step.scroll, step.x, step.y), step.amount);
                yield break;
            }

            if (!string.IsNullOrEmpty(step.tap))
            {
                InputInjector.Tap(ResolveScreenPosition(step.tap, step.x, step.y));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.drag))
            {
                var from = ResolveScreenPosition(step.from, step.fromX, step.fromY);
                var to = ResolveScreenPosition(step.to, step.toX, step.toY);
                yield return InputInjector.Drag(from, to, step.seconds, ParsePointerButton(step.button));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.swipe))
            {
                var from = ResolveScreenPosition(step.from, step.fromX, step.fromY);
                var to = ResolveScreenPosition(step.to, step.toX, step.toY);
                yield return InputInjector.Swipe(from, to, step.seconds);
                yield break;
            }

            if (!string.IsNullOrEmpty(step.pinch))
            {
                yield return InputInjector.Pinch(ResolveScreenPosition(step.center, step.x, step.y), step.fromDistance, step.toDistance, step.seconds);
                yield break;
            }

            if (HasAnyAction(step))
            {
                AddFailure("input", string.Empty, string.Empty, "解釈できる入力がありません。", string.Empty);
            }
        }

        private IEnumerator SaveStepArtifactsCoroutine(UiScenarioStep step, UiSnapshotDocument snapshot, int stepIndex)
        {
            if (!string.IsNullOrEmpty(step.capture))
            {
                var captureFilePath = Path.Combine(_outputDirectory, $"{step.capture}{CaptureFileExtension}");
                yield return CaptureScreenshotCoroutine(captureFilePath, stepIndex);
            }

            if (!string.IsNullOrEmpty(step.snapshot))
            {
                var snapshotFilePath = UiSnapshot.Save(snapshot, _outputDirectory, step.snapshot);
                UnityEngine.Debug.Log($"[UiScenarioRunner] snapshot: {snapshotFilePath}");
            }

            if (step.audit)
            {
                SaveAuditReport(step, stepIndex);
            }
        }

        private IEnumerator CaptureScreenshotCoroutine(string captureFilePath, int stepIndex)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                _warningCount++;
                AddFailure(FailureKindCapture, captureFilePath, string.Empty, "スクリーンサイズが 0 のため撮影できません。", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] capture できませんでした。 step={stepIndex} path={captureFilePath} reason=screenSize");
                yield break;
            }

            // WaitForEndOfFrame + ReadPixels は Game View が再描画されないエディタ状況で永久に戻らない・
            // 描画フレーム外で ReadPixels が失敗する（2026-09-02 実測）。統合前から実績のある ScreenCapture に統一する
            Directory.CreateDirectory(Path.GetDirectoryName(captureFilePath));
            ScreenCapture.CaptureScreenshot(captureFilePath);
            _captureCount++;
            UnityEngine.Debug.Log($"[UiScenarioRunner] capture: {captureFilePath}");
            yield return null;
        }

        private void EvaluateExpectations(UiScenarioStep step, UiSnapshotDocument snapshot, UiSnapshotDiff diff, int forensicsStartCount)
        {
            var expectations = step.expect ?? Array.Empty<ScenarioExpectation>();
            for (var expectationIndex = 0; expectationIndex < expectations.Length; expectationIndex++)
            {
                EvaluateExpectation(expectations[expectationIndex], snapshot, diff, forensicsStartCount);
            }
        }

        private void EvaluateExpectation(ScenarioExpectation expectation, UiSnapshotDocument snapshot, UiSnapshotDiff diff, int forensicsStartCount)
        {
            if (expectation == null || string.IsNullOrEmpty(expectation.kind))
            {
                return;
            }

            switch (expectation.kind)
            {
                case "textVisible": ExpectText(expectation, snapshot, true); return;
                case "textAbsent": ExpectText(expectation, snapshot, false); return;
                case "exists": ExpectElement(expectation, snapshot, true, false); return;
                case "absent": ExpectElement(expectation, snapshot, false, false); return;
                case "interactable": ExpectElement(expectation, snapshot, true, true); return;
                case "disabled": ExpectDisabled(expectation, snapshot); return;
                case "focused": ExpectFocused(expectation, snapshot); return;
                case "sceneIs": ExpectScene(expectation, snapshot); return;
                case "noException": ExpectNoException(expectation, forensicsStartCount); return;
                case "auditClean": ExpectAuditClean(expectation); return;
                case "gameState": ExpectGameState(expectation, snapshot); return;
                case "changed": ExpectChanged(expectation, diff); return;
                case "noDroppedFrames": ExpectNoDroppedFrames(expectation); return;
                case "frameMsP95Below": ExpectFrameMilliseconds(expectation); return;
                case "gcAllocBelow": ExpectGarbageCollectionAlloc(expectation); return;
                case "noGcCollection": ExpectNoGarbageCollection(expectation); return;
                default: AddFailure(expectation.kind, expectation.target, expectation.value, "未対応の expect kind です。", string.Empty); return;
            }
        }

        private void AddStepResult(UiScenarioStep step, int stepIndex, float waitedSeconds, UiSnapshotDocument snapshot)
        {
            var failed = _currentFailures.Count > 0;
            var evidence = failed ? SaveFailureEvidenceSafely(stepIndex, snapshot) : new ScenarioStepEvidence();
            _stepResults.Add(new ScenarioStepResult
            {
                index = stepIndex,
                submit = step.submit ?? string.Empty,
                input = GetInputKind(step),
                status = failed ? StepStatusFail : StepStatusPass,
                waitedSeconds = waitedSeconds,
                failures = _currentFailures.ToArray(),
                evidence = evidence,
            });
        }

        private ScenarioStepEvidence SaveFailureEvidenceSafely(int stepIndex, UiSnapshotDocument snapshot)
        {
            try
            {
                return SaveFailureEvidence(stepIndex, snapshot);
            }
            catch (Exception exception)
            {
                _warningCount++;
                AddFailure(FailureKindEvidence, $"step{stepIndex}", string.Empty, $"{exception.GetType().Name}: {exception.Message}", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 失敗証拠の保存に失敗しました。 step={stepIndex} {exception.GetType().Name}: {exception.Message}");
                return new ScenarioStepEvidence();
            }
        }

        private ScenarioStepEvidence SaveFailureEvidence(int stepIndex, UiSnapshotDocument snapshot)
        {
            var evidenceDirectory = Path.GetDirectoryName(_resultFilePath);
            Directory.CreateDirectory(evidenceDirectory);
            var label = $"step{stepIndex:D2}";
            var captureFilePath = Path.Combine(evidenceDirectory, $"{label}{CaptureFileExtension}");
            var snapshotFilePath = Path.Combine(evidenceDirectory, $"{label}{JsonFileExtension}");
            var savedCaptureFilePath = TryWriteImmediateScreenshot(captureFilePath, stepIndex) ? captureFilePath : string.Empty;
            File.WriteAllText(snapshotFilePath, JsonUtility.ToJson(snapshot, true));
            return new ScenarioStepEvidence { capture = savedCaptureFilePath, snapshot = snapshotFilePath };
        }

        private bool TryWriteImmediateScreenshot(string captureFilePath, int stepIndex)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                _warningCount++;
                AddFailure(FailureKindEvidence, captureFilePath, string.Empty, "スクリーンサイズが 0 のため失敗証拠画像を保存できません。", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 失敗証拠画像を保存できませんでした。 step={stepIndex} path={captureFilePath} reason=screenSize");
                return false;
            }

            // 描画フレーム外の ReadPixels は失敗するため、非同期に書き出す ScreenCapture を使う（次フレーム末に保存される）
            Directory.CreateDirectory(Path.GetDirectoryName(captureFilePath));
            ScreenCapture.CaptureScreenshot(captureFilePath);
            return true;
        }

        private void CompleteInterruptedStep(UiScenarioStep step, int stepIndex, double elapsedSeconds, int stepResultCountBeforeStep)
        {
            var ensuredStep = EnsureStep(step);
            try
            {
                StopRecordingIfNeeded(ensuredStep);
            }
            catch (Exception exception)
            {
                _warningCount++;
                AddFailure(FailureKindException, GetStepFailureTarget(ensuredStep), string.Empty, $"中断ステップの録画停止に失敗しました。 {exception.GetType().Name}: {exception.Message}", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 中断ステップの録画停止に失敗しました。 step={stepIndex} {exception.GetType().Name}: {exception.Message}");
            }

            if (_stepResults.Count > stepResultCountBeforeStep)
            {
                return;
            }

            var snapshot = CaptureSnapshotForInterruptedStep(stepIndex);
            AddStepResult(ensuredStep, stepIndex, (float)elapsedSeconds, snapshot);
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
                return CreateFallbackSnapshot();
            }
        }

        private static UiSnapshotDocument CreateFallbackSnapshot()
        {
            return new UiSnapshotDocument
            {
                capturedAt = DateTimeOffset.Now.ToString("o"),
                frame = Time.frameCount,
                activeScene = SceneManager.GetActiveScene().name,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                focusedPath = string.Empty,
                elements = Array.Empty<UiSnapshotElement>(),
                game = Array.Empty<UiSnapshotGameEntry>(),
            };
        }

        private void Finish(string requestedVerdict)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            StopRecordingIfActive();
            StopInputRecordingIfNeeded();
            StopPerformanceRecordingIfNeeded();
            HideScenarioOverlayIfNeeded();
            SaveResult(requestedVerdict);
            ResultSaved?.Invoke(_resultFilePath);
            Completed?.Invoke();
            Destroy(gameObject);
        }

        private void SaveResult(string requestedVerdict)
        {
            var failedSteps = 0;
            for (var stepResultIndex = 0; stepResultIndex < _stepResults.Count; stepResultIndex++)
            {
                if (_stepResults[stepResultIndex].status == StepStatusFail)
                {
                    failedSteps++;
                }
            }

            var verdict = requestedVerdict == ResultVerdictError ? ResultVerdictError : failedSteps == 0 ? ResultVerdictPass : ResultVerdictFail;
            var result = new ScenarioResult
            {
                scenario = _scenarioName,
                verdict = verdict,
                startedAt = _startedAtText,
                finishedAt = DateTimeOffset.Now.ToString("o"),
                durationSeconds = (float)(Time.realtimeSinceStartupAsDouble - _startedAtRealtime),
                stepCount = _scenario.steps == null ? 0 : _scenario.steps.Length,
                passedSteps = _stepResults.Count - failedSteps,
                failedSteps = failedSteps,
                exceptionCount = _forensics == null ? 0 : _forensics.CapturedCount,
                exceptionSuppressedCount = _forensics == null ? 0 : _forensics.SuppressedCount,
                warningCount = _warningCount,
                droppedFrameCount = _droppedFrameCount,
                steps = _stepResults.ToArray(),
                recordings = _recordingDirectories.ToArray(),
                exceptions = _forensics == null ? Array.Empty<string>() : _forensics.CapturedDirectories,
                performance = _performanceReportPath ?? string.Empty,
                visualRegression = _scenario.visualRegression ?? string.Empty,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_resultFilePath));
            File.WriteAllText(_resultFilePath, JsonUtility.ToJson(result, true));
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

        private void InitializeInputOverlay()
        {
            if (!_scenario.inputOverlay || InputOverlay.IsVisible)
            {
                return;
            }

            InputOverlay.Show();
            _showedScenarioOverlay = true;
        }

        private void HideScenarioOverlayIfNeeded()
        {
            if (!_showedScenarioOverlay)
            {
                return;
            }

            _showedScenarioOverlay = false;
            InputOverlay.Hide();
        }

        private void InitializeInputRecording()
        {
            if (!_scenario.recordInputs)
            {
                return;
            }

            var replayDirectory = Path.Combine(DebugOutputPath.DirectoryPath, ReplayDirectoryName, _scenarioName);
            if (Directory.Exists(replayDirectory))
            {
                Directory.Delete(replayDirectory, true);
            }

            Directory.CreateDirectory(replayDirectory);
            _inputRecorder = new InputRecorder();
            _inputRecorder.StartRecording(replayDirectory);
        }

        private void StopInputRecordingIfNeeded()
        {
            if (_inputRecorder == null)
            {
                return;
            }

            var manifest = _inputRecorder.StopRecording();
            UnityEngine.Debug.Log($"[UiScenarioRunner] replay recorded: inputs={manifest.inputCount}");
            _inputRecorder.Dispose();
            _inputRecorder = null;
        }

        private void InitializePerformanceRecording()
        {
            if (!_scenario.recordPerformance)
            {
                return;
            }

            if (ScenarioHasRecording(_scenario))
            {
                _warningCount++;
                UnityEngine.Debug.LogWarning("[UiScenarioRunner] 録画中の性能計測は録画負荷込みとして扱います。");
            }

            _performanceRecorder = new PerformanceRecorder(_scenarioName, ScenarioHasRecording(_scenario));
            _performanceRecorder.Start();
        }

        private void MarkPerformanceStep(int stepIndex, string actionLabel)
        {
            _performanceRecorder?.MarkStep(stepIndex, actionLabel);
        }

        private void StopPerformanceRecordingIfNeeded()
        {
            if (_performanceRecorder == null)
            {
                return;
            }

            var performanceReport = _performanceRecorder.Stop();
            _performanceReportPath = performanceReport.Save();
            _performanceRecorder.Dispose();
            _performanceRecorder = null;
        }

        private void BeginRecordingIfNeeded(UiScenarioStep step)
        {
            if (step == null || !step.recordStart)
            {
                return;
            }

            if (_videoRecorder != null && _videoRecorder.IsRecording)
            {
                _warningCount++;
                UnityEngine.Debug.LogWarning("[UiScenarioRunner] 録画中のため開始要求を無視しました。");
                return;
            }

            var recordingRootDirectory = Path.Combine(DebugOutputPath.DirectoryPath, RecordingDirectoryName);
            var currentRecordingDirectory = Path.Combine(recordingRootDirectory, CurrentRecordingDirectoryName);
            PrepareCurrentRecordingDirectory(currentRecordingDirectory);
            var framesPerSecond = step.recordFps > 0 ? step.recordFps : RecordingFramesPerSecond;
            _videoRecorder = VideoRecorder.StartRecording(currentRecordingDirectory, TemporaryRecordingName, framesPerSecond, step.recordAudio, ResolveRecordingInputOverlay(step));
            UpdateRecordingContext();
        }

        private void StopRecordingIfNeeded(UiScenarioStep step)
        {
            if (step == null || string.IsNullOrEmpty(step.recordStop) || _videoRecorder == null || !_videoRecorder.IsRecording)
            {
                return;
            }

            FinalizeRecording(step.recordStop);
        }

        private void StopRecordingIfActive()
        {
            if (_videoRecorder == null || !_videoRecorder.IsRecording)
            {
                return;
            }

            FinalizeRecording(TemporaryRecordingName);
        }

        private void FinalizeRecording(string recordingName)
        {
            _droppedFrameCount += _videoRecorder.DroppedFrameCount;
            var recordingResult = _videoRecorder.StopRecording();
            _videoRecorder = null;
            _recordingCount++;

            if (string.IsNullOrEmpty(recordingName))
            {
                return;
            }

            var finalizedRecordingResult = MoveRecordingToFinalDirectory(recordingResult, recordingName);
            _recordingDirectories.Add(finalizedRecordingResult.OutputDirectory);
            ForensicsContext.SetRecording(string.Empty, 0);
            UnityEngine.Debug.Log($"[UiScenarioRunner] recording: frames={finalizedRecordingResult.FrameCount} output={finalizedRecordingResult.OutputDirectory}");
        }

        private void AddRecordingMarkerIfNeeded(string actionLabel)
        {
            if (_videoRecorder == null || !_videoRecorder.IsRecording)
            {
                return;
            }

            _videoRecorder.AddMarker($"step{ForensicsContext.StepIndex} action={actionLabel}");
        }

        private void UpdateRecordingContext()
        {
            if (_videoRecorder == null || !_videoRecorder.IsRecording)
            {
                ForensicsContext.SetRecording(string.Empty, 0);
                return;
            }

            ForensicsContext.SetRecording(TemporaryRecordingName, _videoRecorder.FrameCount);
        }

        private void SaveAuditReport(UiScenarioStep step, int stepIndex)
        {
            var auditReport = UiLayoutAuditor.Audit();
            var auditFileLabel = !string.IsNullOrEmpty(step.capture) ? step.capture : $"step{stepIndex}";
            var auditFilePath = Path.Combine(_outputDirectory, $"{auditFileLabel}{AuditFileNameSuffix}");
            File.WriteAllText(auditFilePath, JsonUtility.ToJson(auditReport, true));
            var entryCount = auditReport.entries == null ? 0 : auditReport.entries.Length;
            _auditCount++;
            UnityEngine.Debug.Log($"[UiScenarioRunner] audit: entries={entryCount} path={auditFilePath}");
        }

        private void ExpectText(ScenarioExpectation expectation, UiSnapshotDocument snapshot, bool shouldExist)
        {
            var found = HasText(snapshot, expectation.value, expectation.scope);
            if (found == shouldExist)
            {
                return;
            }

            AddFailure(expectation.kind, expectation.scope, expectation.value, shouldExist ? "指定テキストが見つかりません。" : "指定テキストが表示されています。", string.Empty);
        }

        private void ExpectElement(ScenarioExpectation expectation, UiSnapshotDocument snapshot, bool shouldExist, bool shouldBeInteractable)
        {
            var element = FindElement(snapshot, expectation.target);
            if (!shouldExist)
            {
                if (element != null)
                {
                    AddFailure(expectation.kind, expectation.target, string.Empty, "要素が存在しています。", string.Empty);
                }

                return;
            }

            if (element == null)
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素が見つかりません。", string.Empty);
                return;
            }

            if (shouldBeInteractable && (!element.interactable || !string.IsNullOrEmpty(element.blockedBy)))
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素が操作可能ではありません。", string.Empty);
            }
        }

        private void ExpectDisabled(ScenarioExpectation expectation, UiSnapshotDocument snapshot)
        {
            var element = FindElement(snapshot, expectation.target);
            if (element == null)
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素が見つかりません。", string.Empty);
                return;
            }

            if (element.interactable && string.IsNullOrEmpty(element.blockedBy))
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素が操作可能です。", string.Empty);
            }
        }

        private void ExpectFocused(ScenarioExpectation expectation, UiSnapshotDocument snapshot)
        {
            var element = FindElement(snapshot, expectation.target);
            if (element == null || !element.focused)
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素にフォーカスがありません。", string.Empty);
            }
        }

        private void ExpectScene(ScenarioExpectation expectation, UiSnapshotDocument snapshot)
        {
            if (!string.Equals(snapshot.activeScene, expectation.value, StringComparison.Ordinal))
            {
                AddFailure(expectation.kind, string.Empty, expectation.value, $"シーンが一致しません。 actual={snapshot.activeScene}", string.Empty);
            }
        }

        private void ExpectNoException(ScenarioExpectation expectation, int forensicsStartCount)
        {
            var capturedCount = _forensics == null ? 0 : _forensics.CapturedCount;
            if (capturedCount <= forensicsStartCount)
            {
                return;
            }

            var directories = _forensics.CapturedDirectories;
            var path = directories.Length == 0 ? string.Empty : directories[directories.Length - 1];
            AddFailure(expectation.kind, string.Empty, string.Empty, "このステップ中に例外またはエラーログが出ました。", path);
        }

        private void ExpectAuditClean(ScenarioExpectation expectation)
        {
            var auditReport = UiLayoutAuditor.Audit();
            var entryCount = auditReport.entries == null ? 0 : auditReport.entries.Length;
            if (entryCount != 0)
            {
                AddFailure(expectation.kind, string.Empty, entryCount.ToString(CultureInfo.InvariantCulture), "レイアウト監査で検出があります。", string.Empty);
            }
        }

        private void ExpectGameState(ScenarioExpectation expectation, UiSnapshotDocument snapshot)
        {
            var actual = FindGameValue(snapshot, expectation.key);
            if (actual == null || CompareGameState(actual, expectation.op, expectation.value))
            {
                return;
            }

            AddFailure(expectation.kind, expectation.key, expectation.value, $"gameState が一致しません。 actual={actual}", string.Empty);
        }

        private void ExpectChanged(ScenarioExpectation expectation, UiSnapshotDiff diff)
        {
            if (diff == null || diff.isEmpty)
            {
                AddFailure(expectation.kind, string.Empty, string.Empty, "スナップショット差分が空です。", string.Empty);
            }
        }

        private void ExpectNoDroppedFrames(ScenarioExpectation expectation)
        {
            var droppedFrameCount = _videoRecorder == null ? 0 : _videoRecorder.DroppedFrameCount;
            if (droppedFrameCount != 0)
            {
                AddFailure(expectation.kind, string.Empty, droppedFrameCount.ToString(CultureInfo.InvariantCulture), "録画中に捨てたフレームがあります。", string.Empty);
            }
        }

        private void ExpectFrameMilliseconds(ScenarioExpectation expectation)
        {
            var report = _performanceRecorder == null ? null : _performanceRecorder.CaptureCurrentStepReport();
            if (report == null)
            {
                return;
            }

            if (!float.TryParse(expectation.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) || report.frameMsP95 >= threshold)
            {
                AddFailure(expectation.kind, string.Empty, expectation.value, $"p95 フレーム時間がしきい値以上です。 actual={report.frameMsP95:F2}", string.Empty);
            }
        }

        private void ExpectGarbageCollectionAlloc(ScenarioExpectation expectation)
        {
            var report = _performanceRecorder == null ? null : _performanceRecorder.CaptureCurrentStepReport();
            if (report == null)
            {
                return;
            }

            if (!long.TryParse(expectation.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold) || report.gcAllocBytes >= threshold)
            {
                AddFailure(expectation.kind, string.Empty, expectation.value, $"GC 割り当てがしきい値以上です。 actual={report.gcAllocBytes}", string.Empty);
            }
        }

        private void ExpectNoGarbageCollection(ScenarioExpectation expectation)
        {
            var report = _performanceRecorder == null ? null : _performanceRecorder.CaptureCurrentStepReport();
            if (report != null && report.gcCollections != 0)
            {
                AddFailure(expectation.kind, string.Empty, report.gcCollections.ToString(CultureInfo.InvariantCulture), "このステップ中に GC が走りました。", string.Empty);
            }
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

        private bool IsReady(UiScenarioStep step, out string failureMessage)
        {
            var anchor = CreateAnchor(step);
            if (!UiInputLocator.IsAnchorSatisfied(anchor))
            {
                failureMessage = "待機条件が満たされませんでした。";
                return false;
            }

            var primaryTarget = GetPrimaryTarget(step);
            if (string.IsNullOrEmpty(primaryTarget))
            {
                failureMessage = string.Empty;
                return true;
            }

            var target = UiInputLocator.FindByPathSegment(primaryTarget);
            if (target == null)
            {
                failureMessage = $"操作対象が現れませんでした。 target={primaryTarget}";
                return false;
            }

            var blockingObject = UiInputLocator.FindBlockingObject(target);
            if (blockingObject != null)
            {
                failureMessage = $"対象が遮られています。 target={primaryTarget} blockedBy={blockingObject.name}";
                return false;
            }

            if (!UiInputLocator.IsInteractable(target))
            {
                failureMessage = $"対象が操作可能ではありません。 target={primaryTarget}";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        private static InputReplayAnchor CreateAnchor(UiScenarioStep step)
        {
            var waitForScene = step.waitForScene;
            if (string.IsNullOrEmpty(waitForScene) && !HasAnyAction(step))
            {
                waitForScene = step.waitScene;
            }

            return new InputReplayAnchor
            {
                waitForText = step.waitForText,
                waitForObject = step.waitForObject,
                waitForFocus = step.waitForFocus,
                waitForScene = waitForScene,
            };
        }

        private static bool HasText(UiSnapshotDocument snapshot, string value, string scope)
        {
            if (snapshot == null || snapshot.elements == null || string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (var elementIndex = 0; elementIndex < snapshot.elements.Length; elementIndex++)
            {
                var element = snapshot.elements[elementIndex];
                if (element == null || string.IsNullOrEmpty(element.label))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(scope) && (string.IsNullOrEmpty(element.path) || !element.path.StartsWith(scope, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (element.label.Contains(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static UiSnapshotElement FindElement(UiSnapshotDocument snapshot, string target)
        {
            if (snapshot == null || snapshot.elements == null || string.IsNullOrEmpty(target))
            {
                return null;
            }

            for (var elementIndex = 0; elementIndex < snapshot.elements.Length; elementIndex++)
            {
                var element = snapshot.elements[elementIndex];
                if (element == null)
                {
                    continue;
                }

                if (string.Equals(element.path, target, StringComparison.Ordinal) || EndsWithPath(element.path, target) || string.Equals(element.name, target, StringComparison.Ordinal))
                {
                    return element;
                }
            }

            return null;
        }

        private static string FindGameValue(UiSnapshotDocument snapshot, string key)
        {
            if (snapshot == null || snapshot.game == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            for (var gameIndex = 0; gameIndex < snapshot.game.Length; gameIndex++)
            {
                var entry = snapshot.game[gameIndex];
                if (entry != null && string.Equals(entry.key, key, StringComparison.Ordinal))
                {
                    return entry.value;
                }
            }

            return null;
        }

        private static bool CompareGameState(string actual, string operation, string expected)
        {
            switch (string.IsNullOrEmpty(operation) ? "eq" : operation)
            {
                case "eq": return string.Equals(actual, expected, StringComparison.Ordinal);
                case "ne": return !string.Equals(actual, expected, StringComparison.Ordinal);
                case "contains": return !string.IsNullOrEmpty(actual) && actual.Contains(expected ?? string.Empty);
                case "lt":
                case "le":
                case "gt":
                case "ge":
                    return CompareNumber(actual, operation, expected);
                default:
                    return false;
            }
        }

        private static bool CompareNumber(string actual, string operation, string expected)
        {
            if (!double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var actualNumber))
            {
                return false;
            }

            if (!double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedNumber))
            {
                return false;
            }

            switch (operation)
            {
                case "lt": return actualNumber < expectedNumber;
                case "le": return actualNumber <= expectedNumber;
                case "gt": return actualNumber > expectedNumber;
                case "ge": return actualNumber >= expectedNumber;
                default: return false;
            }
        }

        private static string GetPrimaryTarget(UiScenarioStep step)
        {
            if (!string.IsNullOrEmpty(step.submit)) { return step.submit; }
            if (!string.IsNullOrEmpty(step.waitForObject)) { return step.waitForObject; }
            if (!string.IsNullOrEmpty(step.click)) { return step.click; }
            if (!string.IsNullOrEmpty(step.tap)) { return step.tap; }
            if (!string.IsNullOrEmpty(step.pointerMove)) { return step.pointerMove; }
            if (!string.IsNullOrEmpty(step.scroll)) { return step.scroll; }
            return string.Empty;
        }

        private static string GetStepFailureTarget(UiScenarioStep step)
        {
            if (step == null)
            {
                return string.Empty;
            }

            var primaryTarget = GetPrimaryTarget(step);
            if (!string.IsNullOrEmpty(primaryTarget))
            {
                return primaryTarget;
            }

            if (!string.IsNullOrEmpty(step.waitScene))
            {
                return step.waitScene;
            }

            if (!string.IsNullOrEmpty(step.waitForScene))
            {
                return step.waitForScene;
            }

            if (!string.IsNullOrEmpty(step.capture))
            {
                return step.capture;
            }

            return string.Empty;
        }

        private static UiScenarioStep EnsureStep(UiScenarioStep step)
        {
            return step ?? new UiScenarioStep();
        }

        private static bool HasAnyAction(UiScenarioStep step)
        {
            return !string.IsNullOrEmpty(step.submit) || IsInputStep(step);
        }

        private static bool IsInputStep(UiScenarioStep step)
        {
            return !string.IsNullOrEmpty(step.press)
                || !string.IsNullOrEmpty(step.hold)
                || !string.IsNullOrEmpty(step.move)
                || !string.IsNullOrEmpty(step.stick)
                || !string.IsNullOrEmpty(step.key)
                || !string.IsNullOrEmpty(step.text)
                || !string.IsNullOrEmpty(step.pointerMove)
                || !string.IsNullOrEmpty(step.click)
                || !string.IsNullOrEmpty(step.drag)
                || !string.IsNullOrEmpty(step.scroll)
                || !string.IsNullOrEmpty(step.tap)
                || !string.IsNullOrEmpty(step.swipe)
                || !string.IsNullOrEmpty(step.pinch);
        }

        private static string GetInputKind(UiScenarioStep step)
        {
            if (!string.IsNullOrEmpty(step.press)) { return "press"; }
            if (!string.IsNullOrEmpty(step.hold)) { return "hold"; }
            if (!string.IsNullOrEmpty(step.move)) { return "move"; }
            if (!string.IsNullOrEmpty(step.stick)) { return "stick"; }
            if (!string.IsNullOrEmpty(step.key)) { return "key"; }
            if (!string.IsNullOrEmpty(step.text)) { return "text"; }
            if (!string.IsNullOrEmpty(step.pointerMove)) { return "pointerMove"; }
            if (!string.IsNullOrEmpty(step.click)) { return "click"; }
            if (!string.IsNullOrEmpty(step.drag)) { return "drag"; }
            if (!string.IsNullOrEmpty(step.scroll)) { return "scroll"; }
            if (!string.IsNullOrEmpty(step.tap)) { return "tap"; }
            if (!string.IsNullOrEmpty(step.swipe)) { return "swipe"; }
            if (!string.IsNullOrEmpty(step.pinch)) { return "pinch"; }
            return string.Empty;
        }

        private static string CreateActionLabel(UiScenarioStep step)
        {
            if (!string.IsNullOrEmpty(step.submit))
            {
                return $"submit:{step.submit}";
            }

            var inputKind = GetInputKind(step);
            if (!string.IsNullOrEmpty(inputKind))
            {
                return $"input:{inputKind}";
            }

            if (step.monkey != null)
            {
                return "monkey";
            }

            return "wait";
        }

        private static int GetSettleFrameCount(UiScenarioStep step)
        {
            if (step.settleFrames > 0)
            {
                return step.settleFrames;
            }

            var needsSettledFrame = !string.IsNullOrEmpty(step.capture)
                || !string.IsNullOrEmpty(step.snapshot)
                || step.audit
                || (step.expect != null && step.expect.Length > 0);
            return needsSettledFrame ? DefaultSettleFrames : 0;
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

        private static bool EndsWithPath(string path, string target)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(target))
            {
                return false;
            }

            return path.EndsWith($"/{target}", StringComparison.Ordinal);
        }

        private static string ResolveScenarioName(UiScenario scenario, string scenarioName)
        {
            if (!string.IsNullOrEmpty(scenarioName))
            {
                return scenarioName;
            }

            if (scenario != null && !string.IsNullOrEmpty(scenario.name))
            {
                return scenario.name;
            }

            return $"scenario-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
        }

        private static string ResolveOutputDirectory(UiScenario scenario)
        {
            if (scenario != null && !string.IsNullOrEmpty(scenario.outputDirectory))
            {
                return scenario.outputDirectory;
            }

            return Path.Combine(DebugOutputPath.DirectoryPath, DefaultOutputDirectoryName);
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "scenario";
            }

            var sanitizedName = fileName;
            var invalidCharacters = Path.GetInvalidFileNameChars();
            for (var characterIndex = 0; characterIndex < invalidCharacters.Length; characterIndex++)
            {
                sanitizedName = sanitizedName.Replace(invalidCharacters[characterIndex], '_');
            }

            return string.IsNullOrEmpty(sanitizedName) ? "scenario" : sanitizedName;
        }

        private static bool ScenarioHasRecording(UiScenario scenario)
        {
            if (scenario == null || scenario.steps == null)
            {
                return false;
            }

            for (var stepIndex = 0; stepIndex < scenario.steps.Length; stepIndex++)
            {
                if (scenario.steps[stepIndex] != null && scenario.steps[stepIndex].recordStart)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ResolveRecordingInputOverlay(UiScenarioStep step)
        {
            if (step != null && step.inputOverlaySpecified)
            {
                return step.inputOverlay;
            }

            if (_scenario.inputOverlaySpecified)
            {
                return _scenario.inputOverlay;
            }

            return true;
        }

        private string ResolveReplayDirectory(string replayName)
        {
            if (Path.IsPathRooted(replayName))
            {
                return replayName;
            }

            return Path.Combine(DebugOutputPath.DirectoryPath, ReplayDirectoryName, replayName);
        }

        private static void PrepareCurrentRecordingDirectory(string currentRecordingDirectory)
        {
            if (Directory.Exists(currentRecordingDirectory))
            {
                Directory.Delete(currentRecordingDirectory, true);
            }

            Directory.CreateDirectory(currentRecordingDirectory);
        }

        private static VideoRecordingResult MoveRecordingToFinalDirectory(VideoRecordingResult recordingResult, string recordingName)
        {
            var recordingRootDirectory = Path.Combine(DebugOutputPath.DirectoryPath, RecordingDirectoryName);
            var finalRecordingDirectory = Path.Combine(recordingRootDirectory, recordingName);
            if (Directory.Exists(finalRecordingDirectory))
            {
                Directory.Delete(finalRecordingDirectory, true);
            }

            Directory.Move(recordingResult.OutputDirectory, finalRecordingDirectory);
            var ffmpegCommand = VideoRecorder.CreateFfmpegCommand(recordingResult.FramesPerSecond, finalRecordingDirectory, recordingName, recordingResult.DurationSeconds, recordingResult.HasAudio);
            var manifestFilePath = Path.Combine(finalRecordingDirectory, VideoRecorder.ManifestFileName);
            RewriteRecordingManifest(manifestFilePath, recordingName, ffmpegCommand);
            return new VideoRecordingResult(recordingName, finalRecordingDirectory, recordingResult.FrameCount, recordingResult.FramesPerSecond, recordingResult.DurationSeconds, manifestFilePath, ffmpegCommand, recordingResult.HasAudio);
        }

        private static void RewriteRecordingManifest(string manifestFilePath, string recordingName, string ffmpegCommand)
        {
            if (!File.Exists(manifestFilePath))
            {
                return;
            }

            var manifest = JsonUtility.FromJson<VideoRecordingManifest>(File.ReadAllText(manifestFilePath));
            if (manifest == null)
            {
                return;
            }

            manifest.name = recordingName;
            manifest.ffmpegCommand = ffmpegCommand;
            File.WriteAllText(manifestFilePath, JsonUtility.ToJson(manifest, true));
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryParseGamepadButton(string buttonName, out GamepadButton button)
        {
            switch (buttonName)
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

        private static bool TryParseKey(string keyName, out Key key)
        {
            foreach (Key candidateKey in Enum.GetValues(typeof(Key)))
            {
                if (string.Equals(candidateKey.ToString(), keyName, StringComparison.OrdinalIgnoreCase))
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
