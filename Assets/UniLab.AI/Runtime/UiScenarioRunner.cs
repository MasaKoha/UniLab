using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        private const int StepTimeoutFrames = 900;
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
            Settling = 2,
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

            if (Time.frameCount - _phaseStartFrame > StepTimeoutFrames)
            {
                _warningCount++;
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] ステップがタイムアウトしました。 index={_currentStepIndex} capture={GetCaptureLabel(_currentStep)} waitScene={GetWaitSceneLabel(_currentStep)}");
                CompleteCurrentStep();
                return;
            }

            if (_phase == StepPhase.WaitingScene)
            {
                if (!IsSceneLoaded(_currentStep.waitScene))
                {
                    return;
                }

                _phase = StepPhase.Settling;
                _phaseStartFrame = Time.frameCount;
                return;
            }

            var settleFrameCount = GetSettleFrameCount(_currentStep);
            if (Time.frameCount - _phaseStartFrame < settleFrameCount)
            {
                return;
            }

            CompleteCurrentStep();
        }

        private void BeginNextStep()
        {
            _currentStep = _remainingSteps.Dequeue();
            _currentStepIndex++;
            _phaseStartFrame = Time.frameCount;
            BeginRecordingIfNeeded(_currentStep);
            AddRecordingMarkerIfNeeded(_currentStep);

            if (!string.IsNullOrEmpty(_currentStep.submit) && !TrySubmit(_currentStep.submit))
            {
                _warningCount++;
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 操作対象が見つかりません。 path={_currentStep.submit}");
            }

            _phase = string.IsNullOrEmpty(_currentStep.waitScene) ? StepPhase.Settling : StepPhase.WaitingScene;
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
            _videoRecorder = VideoRecorder.StartRecording(currentRecordingDirectory, TemporaryRecordingName, RecordingFramesPerSecond);
        }

        private void AddRecordingMarkerIfNeeded(UiScenarioStep step)
        {
            if (_videoRecorder == null || !_videoRecorder.IsRecording)
            {
                return;
            }

            _videoRecorder.AddMarker(CreateStepMarkerLabel(step));
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

            var ffmpegCommand = VideoRecorder.CreateFfmpegCommand(recordingResult.FramesPerSecond, finalRecordingDirectory, recordingName);
            var manifestFilePath = Path.Combine(finalRecordingDirectory, VideoRecorder.ManifestFileName);
            RewriteRecordingManifest(manifestFilePath, recordingName, ffmpegCommand);
            return new VideoRecordingResult(recordingName, finalRecordingDirectory, recordingResult.FrameCount, recordingResult.FramesPerSecond, manifestFilePath, ffmpegCommand);
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

        private string CreateStepMarkerLabel(UiScenarioStep step)
        {
            return $"step{_currentStepIndex} submit={GetSubmitLabel(step)} capture={GetCaptureLabel(step)}";
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

        private static int GetSettleFrameCount(UiScenarioStep step)
        {
            if (step == null || step.settleFrames <= 0)
            {
                return DefaultSettleFrames;
            }

            return step.settleFrames;
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

        private static bool TrySubmit(string objectPath)
        {
            var target = FindByPathSegment(objectPath);
            if (target == null)
            {
                return false;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var eventData = new BaseEventData(eventSystem);
            return ExecuteEvents.Execute(target, eventData, ExecuteEvents.submitHandler);
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
