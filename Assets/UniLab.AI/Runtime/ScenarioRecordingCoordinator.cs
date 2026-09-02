#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// シナリオ実行中の録画・性能計測・入力記録・入力可視化の開始停止をまとめます。
    /// </summary>
    internal sealed class ScenarioRecordingCoordinator
    {
        private const string ReplayDirectoryName = "replays";
        private const string RecordingDirectoryName = "recordings";
        private const string CurrentRecordingDirectoryName = "_current";
        private const string TemporaryRecordingName = "recording";
        private const int RecordingFramesPerSecond = 30;

        private readonly UiScenario _scenario;
        private readonly string _scenarioName;
        private readonly List<string> _recordingDirectories = new List<string>();

        private VideoRecorder _videoRecorder;
        private InputRecorder _inputRecorder;
        private PerformanceRecorder _performanceRecorder;
        private string _performanceReportPath;
        private int _droppedFrameCount;
        private bool _showedScenarioOverlay;

        internal ScenarioRecordingCoordinator(UiScenario scenario, string scenarioName)
        {
            _scenario = scenario;
            _scenarioName = scenarioName;
        }

        internal InputRecorder InputRecorder => _inputRecorder;

        internal VideoRecorder VideoRecorder => _videoRecorder;

        internal PerformanceRecorder PerformanceRecorder => _performanceRecorder;

        internal IReadOnlyList<string> RecordingDirectories => _recordingDirectories;

        internal string PerformanceReportPath => _performanceReportPath;

        internal int DroppedFrameCount => _droppedFrameCount;

        internal void InitializeInputOverlay()
        {
            if (!_scenario.inputOverlay || InputOverlay.IsVisible)
            {
                return;
            }

            InputOverlay.Show();
            _showedScenarioOverlay = true;
        }

        internal void HideScenarioOverlayIfNeeded()
        {
            if (!_showedScenarioOverlay)
            {
                return;
            }

            _showedScenarioOverlay = false;
            InputOverlay.Hide();
        }

        internal void InitializeInputRecording()
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

        internal void StopInputRecordingIfNeeded()
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

        internal void InitializePerformanceRecording(Action incrementWarningCount)
        {
            if (!_scenario.recordPerformance)
            {
                return;
            }

            if (ScenarioHasRecording(_scenario))
            {
                incrementWarningCount();
                UnityEngine.Debug.LogWarning("[UiScenarioRunner] 録画中の性能計測は録画負荷込みとして扱います。");
            }

            _performanceRecorder = new PerformanceRecorder(_scenarioName, ScenarioHasRecording(_scenario));
            _performanceRecorder.Start();
        }

        internal void MarkPerformanceStep(int stepIndex, string actionLabel)
        {
            _performanceRecorder?.MarkStep(stepIndex, actionLabel);
        }

        internal void StopPerformanceRecordingIfNeeded()
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

        internal void BeginRecordingIfNeeded(UiScenarioStep step, Action incrementWarningCount)
        {
            if (step == null || !step.recordStart)
            {
                return;
            }

            if (_videoRecorder != null && _videoRecorder.IsRecording)
            {
                incrementWarningCount();
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

        internal void StopRecordingIfNeeded(UiScenarioStep step)
        {
            if (step == null || string.IsNullOrEmpty(step.recordStop) || _videoRecorder == null || !_videoRecorder.IsRecording)
            {
                return;
            }

            FinalizeRecording(step.recordStop);
        }

        internal void StopRecordingIfActive()
        {
            if (_videoRecorder == null || !_videoRecorder.IsRecording)
            {
                return;
            }

            FinalizeRecording(TemporaryRecordingName);
        }

        internal void FinalizeRecording(string recordingName)
        {
            _droppedFrameCount += _videoRecorder.DroppedFrameCount;
            var recordingResult = _videoRecorder.StopRecording();
            _videoRecorder = null;
            if (string.IsNullOrEmpty(recordingName))
            {
                return;
            }

            var finalizedRecordingResult = MoveRecordingToFinalDirectory(recordingResult, recordingName);
            _recordingDirectories.Add(finalizedRecordingResult.OutputDirectory);
            ForensicsContext.SetRecording(string.Empty, 0);
            UnityEngine.Debug.Log($"[UiScenarioRunner] recording: frames={finalizedRecordingResult.FrameCount} output={finalizedRecordingResult.OutputDirectory}");
        }

        internal void AddRecordingMarkerIfNeeded(string actionLabel)
        {
            if (_videoRecorder == null || !_videoRecorder.IsRecording)
            {
                return;
            }

            _videoRecorder.AddMarker($"step{ForensicsContext.StepIndex} action={actionLabel}");
        }

        internal void UpdateRecordingContext()
        {
            if (_videoRecorder == null || !_videoRecorder.IsRecording)
            {
                ForensicsContext.SetRecording(string.Empty, 0);
                return;
            }

            ForensicsContext.SetRecording(TemporaryRecordingName, _videoRecorder.FrameCount);
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
    }
}
#endif
