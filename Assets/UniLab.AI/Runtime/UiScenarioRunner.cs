#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UniLab.AI
{
    /// <summary>
    /// JSON シナリオに従って UI 操作・撮影・レイアウト監査を自動実行する。
    /// </summary>
    public sealed class UiScenarioRunner : MonoBehaviour
    {
        private const string DefaultOutputDirectoryName = "ui-scenario";
        private const string AuditFileNameSuffix = "-audit.json";
        private const string CaptureFileExtension = ".png";
        private const int DefaultSettleFrames = 30;
        // 準備待ちの上限。フレーム数だと非録画時（高fps）に短すぎるため実時間で持つ
        private const double StepTimeoutSeconds = 30.0;
        private const string RecordingDirectoryName = "recordings";
        private const string CurrentRecordingDirectoryName = "_current";
        private const string TemporaryRecordingName = "recording";
        private const int RecordingFramesPerSecond = 30;

        private readonly Queue<UiScenarioStep> _remainingSteps = new Queue<UiScenarioStep>();

        private UiScenarioStep _currentStep;
        private VideoRecorder _videoRecorder;
        private string _outputDirectory;
        private StepPhase _phase;
        private int _phaseStartFrame;
        private double _stepStartRealtime;
        private int _currentStepIndex;
        private int _captureCount;
        private int _auditCount;
        private int _recordingCount;
        private int _warningCount;
        private bool _isCompleted;

        /// <summary>
        /// シナリオ完了時に通知する。
        /// </summary>
        public event Action Completed;

        private enum StepPhase
        {
            None = 0,
            WaitingScene = 1,
            WaitingReady = 2,
            Settling = 3,
        }

        /// <summary>
        /// 新しい実行インスタンスを生成し、指定シナリオを開始する。
        /// </summary>
        public static UiScenarioRunner Run(UiScenario scenario)
        {
            var runnerObject = new GameObject(nameof(UiScenarioRunner));
            DontDestroyOnLoad(runnerObject);

            var runner = runnerObject.AddComponent<UiScenarioRunner>();
            runner.Initialize(scenario);
            return runner;
        }

        private void Update()
        {
            Drive();
        }

        private void Initialize(UiScenario scenario)
        {
            _outputDirectory = ResolveOutputDirectory(scenario);
            Directory.CreateDirectory(_outputDirectory);

            if (scenario != null && scenario.steps != null)
            {
                for (var stepIndex = 0; stepIndex < scenario.steps.Length; stepIndex++)
                {
                    _remainingSteps.Enqueue(scenario.steps[stepIndex]);
                }
            }

            UnityEngine.Debug.Log($"[UiScenarioRunner] 開始: steps={_remainingSteps.Count} output={_outputDirectory}");
        }

        private void Drive()
        {
            if (_isCompleted)
            {
                return;
            }

            if (_currentStep == null)
            {
                if (_remainingSteps.Count == 0)
                {
                    Finish();
                    return;
                }

                BeginNextStep();
                return;
            }

            if (_phase == StepPhase.WaitingScene)
            {
                DriveWaitingScene();
                return;
            }

            if (_phase == StepPhase.WaitingReady)
            {
                DriveWaitingReady();
                return;
            }

            if (Time.frameCount - _phaseStartFrame < GetSettleFrameCount(_currentStep))
            {
                return;
            }

            CompleteCurrentStep();
        }

        private void DriveWaitingScene()
        {
            if (IsSceneLoaded(_currentStep.waitScene))
            {
                EnterPhase(StepPhase.Settling);
                return;
            }

            if (HasStepTimedOut())
            {
                _warningCount++;
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] シーンのロード待ちがタイムアウトしました。 index={_currentStepIndex} waitScene={GetWaitSceneLabel(_currentStep)}");
                EnterPhase(StepPhase.Settling);
            }
        }

        /// <summary>
        /// 対象が「プレイヤーが押せる状態」になった瞬間に送出する。
        /// フレーム数で待たないことで、動画に写る間がゲーム本来の応答時間そのものになる
        /// （観測器がゲームの見え方を変えない）。遮蔽中は送出しないため、モーダル越しに押す事故も起きない。
        /// </summary>
        private void DriveWaitingReady()
        {
            if (string.IsNullOrEmpty(_currentStep.submit))
            {
                EnterPhase(string.IsNullOrEmpty(_currentStep.waitScene) ? StepPhase.Settling : StepPhase.WaitingScene);
                return;
            }

            var target = FindByPathSegment(_currentStep.submit);
            var blockingObject = target == null ? null : FindBlockingObject(target);
            // 最前面にあっても、開くアニメーション中は CanvasGroup が interactable=false で押せない。
            // Button.OnSubmit はその状態を黙って捨てるため、押せる状態になるまで待つ
            var isInteractable = target != null && IsInteractable(target);
            var isReady = target != null && blockingObject == null && isInteractable;
            if (isReady)
            {
                var waitedSeconds = Time.realtimeSinceStartupAsDouble - _stepStartRealtime;
                AddRecordingMarkerIfNeeded(_currentStep, waitedSeconds);
                if (!TrySubmit(target))
                {
                    _warningCount++;
                    UnityEngine.Debug.LogWarning($"[UiScenarioRunner] submit を受け取る要素がありません。 path={_currentStep.submit}");
                }

                EnterPhase(string.IsNullOrEmpty(_currentStep.waitScene) ? StepPhase.Settling : StepPhase.WaitingScene);
                return;
            }

            if (!HasStepTimedOut())
            {
                return;
            }

            _warningCount++;
            if (target == null)
            {
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 操作対象が現れませんでした。 index={_currentStepIndex} path={_currentStep.submit}");
            }
            else if (blockingObject != null)
            {
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 対象が遮られたままでした。送出を見送ります。 index={_currentStepIndex} path={_currentStep.submit} blockedBy={blockingObject.name}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 対象が操作可能になりませんでした。送出を見送ります。 index={_currentStepIndex} path={_currentStep.submit}");
            }

            EnterPhase(StepPhase.Settling);
        }

        private void EnterPhase(StepPhase phase)
        {
            _phase = phase;
            _phaseStartFrame = Time.frameCount;
        }

        private bool HasStepTimedOut()
        {
            return Time.realtimeSinceStartupAsDouble - _stepStartRealtime > StepTimeoutSeconds;
        }

        private void BeginNextStep()
        {
            _currentStep = _remainingSteps.Dequeue();
            _currentStepIndex++;
            _stepStartRealtime = Time.realtimeSinceStartupAsDouble;
            BeginRecordingIfNeeded(_currentStep);

            if (string.IsNullOrEmpty(_currentStep.submit))
            {
                // 操作の無いステップはマーカーだけ打つ（撮影・録画停止・待機の位置が動画から辿れるように）
                AddRecordingMarkerIfNeeded(_currentStep, 0.0);
            }

            // waitScene は「操作した結果のシーン到着」を待つ条件。操作の前に待つと、
            // 操作しないと始まらない遷移を永遠に待ってしまう
            EnterPhase(StepPhase.WaitingReady);
        }

        private void CompleteCurrentStep()
        {
            if (!string.IsNullOrEmpty(_currentStep.capture))
            {
                var captureFilePath = Path.Combine(_outputDirectory, $"{_currentStep.capture}{CaptureFileExtension}");
                ScreenCapture.CaptureScreenshot(captureFilePath);
                _captureCount++;
                UnityEngine.Debug.Log($"[UiScenarioRunner] capture: {captureFilePath}");
            }

            if (_currentStep.audit)
            {
                SaveAuditReport();
            }

            StopRecordingIfNeeded(_currentStep);

            _currentStep = null;
            _phase = StepPhase.None;
        }

        private void SaveAuditReport()
        {
            var auditReport = UiLayoutAuditor.Audit();
            var auditFileLabel = !string.IsNullOrEmpty(_currentStep.capture) ? _currentStep.capture : $"step{_currentStepIndex}";
            var auditFilePath = Path.Combine(_outputDirectory, $"{auditFileLabel}{AuditFileNameSuffix}");
            var auditJson = JsonUtility.ToJson(auditReport, true);
            File.WriteAllText(auditFilePath, auditJson);

            var entryCount = auditReport.entries == null ? 0 : auditReport.entries.Length;
            _auditCount++;
            UnityEngine.Debug.Log($"[UiScenarioRunner] audit: entries={entryCount} path={auditFilePath}");
        }

        private void Finish()
        {
            if (_isCompleted)
            {
                return;
            }

            _isCompleted = true;
            StopRecordingIfActive();
            UnityEngine.Debug.Log($"[UiScenarioRunner] 完了: capture {_captureCount} 枚 / audit {_auditCount} 回 / recording {_recordingCount} 本 / 警告 {_warningCount} 件");
            Completed?.Invoke();
            Destroy(gameObject);
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
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 録画中のため開始要求を無視しました。 index={_currentStepIndex}");
                return;
            }

            var recordingRootDirectory = Path.Combine(DebugOutputPath.DirectoryPath, RecordingDirectoryName);
            var currentRecordingDirectory = Path.Combine(recordingRootDirectory, CurrentRecordingDirectoryName);
            PrepareCurrentRecordingDirectory(currentRecordingDirectory);
            var recordingFramesPerSecond = step.recordFps > 0 ? step.recordFps : RecordingFramesPerSecond;
            _videoRecorder = VideoRecorder.StartRecording(currentRecordingDirectory, TemporaryRecordingName, recordingFramesPerSecond, step.recordAudio);
        }

        private void AddRecordingMarkerIfNeeded(UiScenarioStep step, double waitedSeconds)
        {
            if (_videoRecorder == null || !_videoRecorder.IsRecording)
            {
                return;
            }

            _videoRecorder.AddMarker(CreateStepMarkerLabel(step, waitedSeconds));
        }

        private void StopRecordingIfNeeded(UiScenarioStep step)
        {
            if (step == null || string.IsNullOrEmpty(step.recordStop))
            {
                return;
            }

            if (_videoRecorder == null || !_videoRecorder.IsRecording)
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
            var recordingResult = _videoRecorder.StopRecording();
            _videoRecorder = null;
            _recordingCount++;

            if (string.IsNullOrEmpty(recordingName))
            {
                return;
            }

            var finalizedRecordingResult = MoveRecordingToFinalDirectory(recordingResult, recordingName);
            UnityEngine.Debug.Log($"[UiScenarioRunner] recording: frames={finalizedRecordingResult.FrameCount} output={finalizedRecordingResult.OutputDirectory} ffmpeg={finalizedRecordingResult.FfmpegCommand}");
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

            var manifestJson = File.ReadAllText(manifestFilePath);
            var manifest = JsonUtility.FromJson<VideoRecordingManifest>(manifestJson);
            if (manifest == null)
            {
                return;
            }

            manifest.name = recordingName;
            manifest.ffmpegCommand = ffmpegCommand;
            File.WriteAllText(manifestFilePath, JsonUtility.ToJson(manifest, true));
        }

        /// <summary>waited は「対象が押せる状態になるまで待った実時間」。ゲームの応答時間の計測値になる。</summary>
        private string CreateStepMarkerLabel(UiScenarioStep step, double waitedSeconds)
        {
            return $"step{_currentStepIndex} submit={GetSubmitLabel(step)} capture={GetCaptureLabel(step)} waited={waitedSeconds:F2}s";
        }

        private static string GetSubmitLabel(UiScenarioStep step)
        {
            if (step == null || string.IsNullOrEmpty(step.submit))
            {
                return "-";
            }

            return step.submit;
        }

        private static string ResolveOutputDirectory(UiScenario scenario)
        {
            if (scenario != null && !string.IsNullOrEmpty(scenario.outputDirectory))
            {
                return scenario.outputDirectory;
            }

            return Path.Combine(DebugOutputPath.DirectoryPath, DefaultOutputDirectoryName);
        }

        /// <summary>
        /// 操作後に待つフレーム数。撮影・監査を行うステップだけ既定で待つ（動きが収まった絵を残すため）。
        /// それ以外は 0 で、次ステップの準備待ちがゲーム本来の間を作る。
        /// </summary>
        private static int GetSettleFrameCount(UiScenarioStep step)
        {
            if (step.settleFrames > 0)
            {
                return step.settleFrames;
            }

            var needsSettledFrame = !string.IsNullOrEmpty(step.capture) || step.audit;
            return needsSettledFrame ? DefaultSettleFrames : 0;
        }

        private static string GetCaptureLabel(UiScenarioStep step)
        {
            if (step == null || string.IsNullOrEmpty(step.capture))
            {
                return "-";
            }

            return step.capture;
        }

        private static string GetWaitSceneLabel(UiScenarioStep step)
        {
            if (step == null || string.IsNullOrEmpty(step.waitScene))
            {
                return "-";
            }

            return step.waitScene;
        }

        private static bool IsSceneLoaded(string sceneName)
        {
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.isLoaded && scene.name == sceneName)
                {
                    return true;
                }
            }

            return false;
        }


        private static bool TrySubmit(GameObject target)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var eventData = new BaseEventData(eventSystem);
            return ExecuteEvents.Execute(target, eventData, ExecuteEvents.submitHandler);
        }

        /// <summary>
        /// 対象の中心へレイキャストし、最前面が対象自身でも子孫でもなければ、その遮蔽物を返す。
        /// 判定できないときは null（遮蔽なし扱い）を返す。
        /// </summary>
        private static GameObject FindBlockingObject(GameObject target)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            var targetRectTransform = target.transform as RectTransform;
            if (targetRectTransform == null)
            {
                return null;
            }

            var screenPoint = RectTransformUtility.WorldToScreenPoint(ResolveCanvasCamera(target), targetRectTransform.position);
            var pointerEventData = new PointerEventData(eventSystem)
            {
                position = screenPoint,
            };

            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            if (raycastResults.Count == 0)
            {
                return null;
            }

            var frontMostObject = raycastResults[0].gameObject;
            if (IsSelfOrDescendant(frontMostObject, target))
            {
                return null;
            }

            return frontMostObject;
        }

        private static Camera ResolveCanvasCamera(GameObject target)
        {
            // 汎用の検証ツールのため対象の Canvas を結線で持てない。ステップごとに1回だけの探索であり
            // 毎フレーム経路ではないため、ここでは GetComponentInParent を許容する
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }

        /// <summary>
        /// Selectable を持つ対象は、親 CanvasGroup まで含めて操作可能か判定する。
        /// Selectable を持たない対象（submit ハンドラだけの GameObject 等）は判定できないため true とする。
        /// </summary>
        private static bool IsInteractable(GameObject target)
        {
            // 汎用の検証ツールのため結線で持てない。ステップごとの判定であり毎フレーム経路ではないので許容する
            var selectable = target.GetComponent<Selectable>();
            if (selectable == null)
            {
                return true;
            }

            return selectable.IsInteractable();
        }

        private static bool IsSelfOrDescendant(GameObject candidate, GameObject target)
        {
            var currentTransform = candidate.transform;
            while (currentTransform != null)
            {
                if (currentTransform.gameObject == target)
                {
                    return true;
                }

                currentTransform = currentTransform.parent;
            }

            return false;
        }

        private static GameObject FindByPathSegment(string objectPath)
        {
            var pathSegments = objectPath.Split('/');
            var candidateTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (var candidateIndex = 0; candidateIndex < candidateTransforms.Length; candidateIndex++)
            {
                var candidateTransform = candidateTransforms[candidateIndex];
                if (candidateTransform.name != pathSegments[pathSegments.Length - 1])
                {
                    continue;
                }

                if (pathSegments.Length == 1)
                {
                    return candidateTransform.gameObject;
                }

                if (DoesPathMatch(candidateTransform, pathSegments))
                {
                    return candidateTransform.gameObject;
                }
            }

            return null;
        }

        private static bool DoesPathMatch(Transform targetTransform, string[] pathSegments)
        {
            var currentTransform = targetTransform;
            for (var pathIndex = pathSegments.Length - 1; pathIndex >= 0; pathIndex--)
            {
                if (currentTransform == null)
                {
                    return false;
                }

                if (currentTransform.name != pathSegments[pathIndex])
                {
                    return false;
                }

                currentTransform = currentTransform.parent;
            }

            return true;
        }
    }
}
#endif
