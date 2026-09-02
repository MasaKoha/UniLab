#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// シナリオ実行中のスクリーンショット・監査・結果 JSON をファイルへ保存します。
    /// </summary>
    internal sealed class ScenarioArtifactWriter
    {
        private const string CaptureFileExtension = ".png";
        private const string JsonFileExtension = ".json";
        private const string AuditFileNameSuffix = "-audit.json";
        private const string FailureKindCapture = "capture";
        private const string FailureKindEvidence = "evidence";
        private const string ResultVerdictError = "error";
        private const string ResultVerdictPass = "pass";
        private const string ResultVerdictFail = "fail";
        private const string StepStatusFail = "fail";

        private readonly string _outputDirectory;
        private readonly string _resultFilePath;

        internal ScenarioArtifactWriter(string outputDirectory, string resultFilePath)
        {
            _outputDirectory = outputDirectory;
            _resultFilePath = resultFilePath;
        }

        internal IEnumerator SaveStepArtifactsCoroutine(UiScenarioStep step, UiSnapshotDocument snapshot, int stepIndex, Action<string, string, string, string, string> addFailure, Action incrementWarningCount, Action incrementCaptureCount, Action incrementAuditCount)
        {
            if (!string.IsNullOrEmpty(step.capture))
            {
                var captureFilePath = Path.Combine(_outputDirectory, $"{step.capture}{CaptureFileExtension}");
                yield return CaptureScreenshotCoroutine(captureFilePath, stepIndex, addFailure, incrementWarningCount, incrementCaptureCount);
            }

            if (!string.IsNullOrEmpty(step.snapshot))
            {
                var snapshotFilePath = UiSnapshot.Save(snapshot, _outputDirectory, step.snapshot);
                UnityEngine.Debug.Log($"[UiScenarioRunner] snapshot: {snapshotFilePath}");
            }

            if (step.audit)
            {
                SaveAuditReport(step, stepIndex, incrementAuditCount);
            }
        }

        internal ScenarioStepEvidence SaveFailureEvidenceSafely(int stepIndex, UiSnapshotDocument snapshot, Action<string, string, string, string, string> addFailure, Action incrementWarningCount)
        {
            try
            {
                return SaveFailureEvidence(stepIndex, snapshot, addFailure, incrementWarningCount);
            }
            catch (Exception exception)
            {
                incrementWarningCount();
                addFailure(FailureKindEvidence, $"step{stepIndex}", string.Empty, $"{exception.GetType().Name}: {exception.Message}", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 失敗証拠の保存に失敗しました。 step={stepIndex} {exception.GetType().Name}: {exception.Message}");
                return new ScenarioStepEvidence();
            }
        }

        internal void SaveResult(string requestedVerdict, UiScenario scenario, string scenarioName, string startedAtText, double startedAtRealtime, IReadOnlyList<ScenarioStepResult> stepResults, IReadOnlyList<string> recordingDirectories, ExceptionForensics forensics, int warningCount, int droppedFrameCount, string performanceReportPath)
        {
            var failedSteps = 0;
            for (var stepResultIndex = 0; stepResultIndex < stepResults.Count; stepResultIndex++)
            {
                if (stepResults[stepResultIndex].status == StepStatusFail)
                {
                    failedSteps++;
                }
            }

            var verdict = requestedVerdict == ResultVerdictError ? ResultVerdictError : failedSteps == 0 ? ResultVerdictPass : ResultVerdictFail;
            var result = new ScenarioResult
            {
                scenario = scenarioName,
                verdict = verdict,
                startedAt = startedAtText,
                finishedAt = DateTimeOffset.Now.ToString("o"),
                durationSeconds = (float)(Time.realtimeSinceStartupAsDouble - startedAtRealtime),
                stepCount = scenario.steps == null ? 0 : scenario.steps.Length,
                passedSteps = stepResults.Count - failedSteps,
                failedSteps = failedSteps,
                exceptionCount = forensics == null ? 0 : forensics.CapturedCount,
                exceptionSuppressedCount = forensics == null ? 0 : forensics.SuppressedCount,
                warningCount = warningCount,
                droppedFrameCount = droppedFrameCount,
                steps = ToStepResultArray(stepResults),
                recordings = ToStringArray(recordingDirectories),
                exceptions = forensics == null ? Array.Empty<string>() : forensics.CapturedDirectories,
                performance = performanceReportPath ?? string.Empty,
                visualRegression = scenario.visualRegression ?? string.Empty,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_resultFilePath));
            File.WriteAllText(_resultFilePath, JsonUtility.ToJson(result, true));
        }

        private IEnumerator CaptureScreenshotCoroutine(string captureFilePath, int stepIndex, Action<string, string, string, string, string> addFailure, Action incrementWarningCount, Action incrementCaptureCount)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                incrementWarningCount();
                addFailure(FailureKindCapture, captureFilePath, string.Empty, "スクリーンサイズが 0 のため撮影できません。", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] capture できませんでした。 step={stepIndex} path={captureFilePath} reason=screenSize");
                yield break;
            }

            // WaitForEndOfFrame + ReadPixels は Game View が再描画されないエディタ状況で永久に戻らない・
            // 描画フレーム外で ReadPixels が失敗する（2026-09-02 実測）。統合前から実績のある ScreenCapture に統一する
            Directory.CreateDirectory(Path.GetDirectoryName(captureFilePath));
            ScreenCapture.CaptureScreenshot(captureFilePath);
            incrementCaptureCount();
            UnityEngine.Debug.Log($"[UiScenarioRunner] capture: {captureFilePath}");
            yield return null;
        }

        private ScenarioStepEvidence SaveFailureEvidence(int stepIndex, UiSnapshotDocument snapshot, Action<string, string, string, string, string> addFailure, Action incrementWarningCount)
        {
            var evidenceDirectory = Path.GetDirectoryName(_resultFilePath);
            Directory.CreateDirectory(evidenceDirectory);
            var label = $"step{stepIndex:D2}";
            var captureFilePath = Path.Combine(evidenceDirectory, $"{label}{CaptureFileExtension}");
            var snapshotFilePath = Path.Combine(evidenceDirectory, $"{label}{JsonFileExtension}");
            var savedCaptureFilePath = TryWriteImmediateScreenshot(captureFilePath, stepIndex, addFailure, incrementWarningCount) ? captureFilePath : string.Empty;
            File.WriteAllText(snapshotFilePath, JsonUtility.ToJson(snapshot, true));
            return new ScenarioStepEvidence { capture = savedCaptureFilePath, snapshot = snapshotFilePath };
        }

        private bool TryWriteImmediateScreenshot(string captureFilePath, int stepIndex, Action<string, string, string, string, string> addFailure, Action incrementWarningCount)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                incrementWarningCount();
                addFailure(FailureKindEvidence, captureFilePath, string.Empty, "スクリーンサイズが 0 のため失敗証拠画像を保存できません。", string.Empty);
                UnityEngine.Debug.LogWarning($"[UiScenarioRunner] 失敗証拠画像を保存できませんでした。 step={stepIndex} path={captureFilePath} reason=screenSize");
                return false;
            }

            // 描画フレーム外の ReadPixels は失敗するため、非同期に書き出す ScreenCapture を使う（次フレーム末に保存される）
            Directory.CreateDirectory(Path.GetDirectoryName(captureFilePath));
            ScreenCapture.CaptureScreenshot(captureFilePath);
            return true;
        }

        private void SaveAuditReport(UiScenarioStep step, int stepIndex, Action incrementAuditCount)
        {
            var auditReport = UiLayoutAuditor.Audit();
            var auditFileLabel = !string.IsNullOrEmpty(step.capture) ? step.capture : $"step{stepIndex}";
            var auditFilePath = Path.Combine(_outputDirectory, $"{auditFileLabel}{AuditFileNameSuffix}");
            File.WriteAllText(auditFilePath, JsonUtility.ToJson(auditReport, true));
            var entryCount = auditReport.entries == null ? 0 : auditReport.entries.Length;
            incrementAuditCount();
            UnityEngine.Debug.Log($"[UiScenarioRunner] audit: entries={entryCount} path={auditFilePath}");
        }

        private static ScenarioStepResult[] ToStepResultArray(IReadOnlyList<ScenarioStepResult> stepResults)
        {
            var results = new ScenarioStepResult[stepResults.Count];
            for (var resultIndex = 0; resultIndex < stepResults.Count; resultIndex++)
            {
                results[resultIndex] = stepResults[resultIndex];
            }

            return results;
        }

        private static string[] ToStringArray(IReadOnlyList<string> values)
        {
            var results = new string[values.Count];
            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                results[valueIndex] = values[valueIndex];
            }

            return results;
        }
    }
}
#endif
