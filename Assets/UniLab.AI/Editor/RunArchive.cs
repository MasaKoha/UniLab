using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// 散在する成果物をラン単位へ再配置し、スマホ閲覧と過去比較がディレクトリ 1 つで完結する状態を作る。
    /// </summary>
    public static class RunArchive
    {
        private const string VerificationRunsDirectoryName = "VerificationRuns";
        private const string RunDirectoryPrefix = "run-";
        private const string MetaFileName = "meta.json";
        private const string IndexFileName = "index.json";
        private const string ScenarioResultFileName = "scenario-result.json";
        private const string PlayerLogFileName = "player-log.log";
        private const string CapturesDirectoryName = "captures";
        private const string SnapshotsDirectoryName = "snapshots";
        private const string RecordingsDirectoryName = "recordings";
        private const string ForensicsDirectoryName = "forensics";
        private const string VisualRegressionDirectoryName = "visual-regression";
        private const string MonkeyDirectoryName = "monkey";
        private const string PerformanceFileName = "performance.json";
        private const string DefaultScenarioCaptureDirectoryName = "ui-scenario";
        private const string SnapshotSourceDirectoryName = "snapshots";
        private const string ScenarioResultsDirectoryName = "scenario-results";
        private const string PerformanceSourceDirectoryName = "performance";
        private const string RecordingSourceDirectoryName = "recordings";
        private const string ForensicsSourceDirectoryName = "forensics";
        private const string MonkeySourceDirectoryName = "monkey";
        private const string VisualRegressionSourceDirectoryName = "visual-regression";
        private const string PlayerLogFileNamePrefix = "player-log-";
        private const string AuditFileNameSuffix = "-audit.json";
        private const string ReportFileName = "report.json";
        private const string SnapshotCompactTextExtension = ".txt";
        private const string CaptureExtension = ".png";
        private const string GitExecutableName = "git";
        private const string TimestampFormat = "yyyyMMdd-HHmmss";
        private const string TimestampWithMillisecondsFormat = "yyyyMMdd-HHmmss-fff";
        private const double ArtifactWindowPaddingSeconds = 300.0;

        /// <summary>
        /// 直近成果物を起点にランを再構成し、既存ツールへ手を入れずにラン単位の確認導線を作る。
        /// </summary>
        public static string CreateLatest()
        {
            var scenarioResultsDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, ScenarioResultsDirectoryName);
            var latestScenarioResultPath = FindLatestFile(scenarioResultsDirectoryPath, "*.json");
            return CreateFromScenarioResult(latestScenarioResultPath);
        }

        /// <summary>
        /// シナリオ結果を起点に成果物を集約し直し、過去ランも同じ形式へ揃えられるようにする。
        /// </summary>
        public static string CreateFromScenarioResult(string scenarioResultPath)
        {
            var scenarioResult = LoadScenarioResult(scenarioResultPath);
            var projectRootDirectoryPath = GetProjectRootDirectoryPath();
            var verificationRunsDirectoryPath = Path.Combine(projectRootDirectoryPath, VerificationRunsDirectoryName);
            Directory.CreateDirectory(verificationRunsDirectoryPath);

            var anchorTime = ResolveAnchorTime(scenarioResultPath, scenarioResult);
            var runStartedAt = ResolveStartedAt(anchorTime, scenarioResult);
            var runFinishedAt = ResolveFinishedAt(anchorTime, scenarioResult, runStartedAt);
            var runDirectoryPath = CreateUniqueRunDirectory(verificationRunsDirectoryPath, runStartedAt);

            var captureOutputDirectoryPath = Path.Combine(runDirectoryPath, CapturesDirectoryName);
            var snapshotOutputDirectoryPath = Path.Combine(runDirectoryPath, SnapshotsDirectoryName);
            var recordingOutputDirectoryPath = Path.Combine(runDirectoryPath, RecordingsDirectoryName);
            var forensicOutputDirectoryPath = Path.Combine(runDirectoryPath, ForensicsDirectoryName);
            var monkeyOutputDirectoryPath = Path.Combine(runDirectoryPath, MonkeyDirectoryName);
            var visualRegressionOutputDirectoryPath = Path.Combine(runDirectoryPath, VisualRegressionDirectoryName);

            Directory.CreateDirectory(captureOutputDirectoryPath);
            Directory.CreateDirectory(snapshotOutputDirectoryPath);
            Directory.CreateDirectory(recordingOutputDirectoryPath);
            Directory.CreateDirectory(forensicOutputDirectoryPath);

            var evidenceCaptureMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var evidenceSnapshotMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var copiedCaptureFileCount = CopyScenarioCaptures(runStartedAt, runFinishedAt, captureOutputDirectoryPath);
            var copiedAuditFileCount = CountFiles(captureOutputDirectoryPath, $"*{AuditFileNameSuffix}");
            var auditFindingCount = CountAuditFindings(captureOutputDirectoryPath);

            CopyScenarioEvidenceFiles(scenarioResult, captureOutputDirectoryPath, snapshotOutputDirectoryPath, evidenceCaptureMap, evidenceSnapshotMap);
            CopyWindowSnapshots(runStartedAt, runFinishedAt, snapshotOutputDirectoryPath, evidenceSnapshotMap);

            var copiedRecordingNames = CopyRecordings(scenarioResult, runStartedAt, runFinishedAt, recordingOutputDirectoryPath);
            var droppedFrameCount = CountDroppedFrames(recordingOutputDirectoryPath);
            var copiedForensicDirectoryNames = CopyForensics(scenarioResult, runStartedAt, runFinishedAt, forensicOutputDirectoryPath);
            CopyMonkeyRuns(runStartedAt, runFinishedAt, monkeyOutputDirectoryPath);
            var visualRegressionSummary = CopyVisualRegression(runStartedAt, runFinishedAt, visualRegressionOutputDirectoryPath);
            var performanceSummary = CopyPerformance(runStartedAt, runFinishedAt, Path.Combine(runDirectoryPath, PerformanceFileName));
            CopyPlayerLog(runStartedAt, runFinishedAt, Path.Combine(runDirectoryPath, PlayerLogFileName));

            var archivedScenarioResult = RewriteScenarioResult(
                scenarioResult,
                evidenceCaptureMap,
                evidenceSnapshotMap,
                copiedRecordingNames,
                copiedForensicDirectoryNames);
            SaveScenarioResult(runDirectoryPath, archivedScenarioResult);

            copiedCaptureFileCount = CountFiles(captureOutputDirectoryPath, "*.png");
            copiedAuditFileCount = CountFiles(captureOutputDirectoryPath, $"*{AuditFileNameSuffix}");
            auditFindingCount = CountAuditFindings(captureOutputDirectoryPath);

            var meta = BuildMeta(
                archivedScenarioResult,
                runStartedAt,
                runFinishedAt,
                copiedCaptureFileCount,
                copiedAuditFileCount,
                auditFindingCount,
                droppedFrameCount,
                copiedRecordingNames,
                copiedForensicDirectoryNames.Length,
                visualRegressionSummary,
                performanceSummary);
            var metaFilePath = Path.Combine(runDirectoryPath, MetaFileName);
            File.WriteAllText(metaFilePath, JsonUtility.ToJson(meta, true));
            RebuildIndex();
            return runDirectoryPath;
        }

        /// <summary>
        /// 配下ランから索引を再生成し、ギャラリーが常に最新の一覧を読める状態を保つ。
        /// </summary>
        public static string RebuildIndex()
        {
            var verificationRunsDirectoryPath = Path.Combine(GetProjectRootDirectoryPath(), VerificationRunsDirectoryName);
            Directory.CreateDirectory(verificationRunsDirectoryPath);

            var runDirectoryPaths = Directory.GetDirectories(verificationRunsDirectoryPath, $"{RunDirectoryPrefix}*", SearchOption.TopDirectoryOnly);
            Array.Sort(runDirectoryPaths, StringComparer.Ordinal);
            Array.Reverse(runDirectoryPaths);

            var entries = new List<RunArchiveIndexEntry>(runDirectoryPaths.Length);
            for (var directoryIndex = 0; directoryIndex < runDirectoryPaths.Length; directoryIndex++)
            {
                var runDirectoryPath = runDirectoryPaths[directoryIndex];
                var metaFilePath = Path.Combine(runDirectoryPath, MetaFileName);
                if (!File.Exists(metaFilePath))
                {
                    continue;
                }

                var metaJson = File.ReadAllText(metaFilePath);
                var meta = JsonUtility.FromJson<RunArchiveMeta>(metaJson);
                if (meta == null)
                {
                    continue;
                }

                var runDirectoryName = Path.GetFileName(runDirectoryPath);
                var relativeRunPath = NormalizePathForJson(runDirectoryName);
                var relativeMetaPath = NormalizePathForJson(Path.Combine(runDirectoryName, MetaFileName));
                entries.Add(new RunArchiveIndexEntry(
                    runDirectoryName,
                    relativeRunPath,
                    relativeMetaPath,
                    meta.scenario,
                    meta.verdict,
                    meta.startedAt,
                    meta.finishedAt,
                    meta.durationSeconds));
            }

            var index = new RunArchiveIndex(DateTimeOffset.Now.ToString("o"), entries.ToArray());
            var indexFilePath = Path.Combine(verificationRunsDirectoryPath, IndexFileName);
            File.WriteAllText(indexFilePath, JsonUtility.ToJson(index, true));
            return indexFilePath;
        }

        private static RunArchiveMeta BuildMeta(
            RunArchiveScenarioResult scenarioResult,
            DateTimeOffset runStartedAt,
            DateTimeOffset runFinishedAt,
            int captureCount,
            int auditCount,
            int auditFindingCount,
            int droppedFrameCount,
            string[] recordingNames,
            int forensicDirectoryCount,
            RunArchiveVisualRegressionSummary visualRegressionSummary,
            RunArchivePerformanceSummary performanceSummary)
        {
            var scenarioName = scenarioResult == null ? string.Empty : scenarioResult.scenario;
            var verdict = scenarioResult == null ? string.Empty : scenarioResult.verdict;
            var startedAtText = scenarioResult == null || string.IsNullOrEmpty(scenarioResult.startedAt)
                ? runStartedAt.ToString("o")
                : scenarioResult.startedAt;
            var finishedAtText = scenarioResult == null || string.IsNullOrEmpty(scenarioResult.finishedAt)
                ? runFinishedAt.ToString("o")
                : scenarioResult.finishedAt;
            var durationSeconds = scenarioResult == null || scenarioResult.durationSeconds <= 0.0f
                ? (float)Math.Max(0.0, (runFinishedAt - runStartedAt).TotalSeconds)
                : scenarioResult.durationSeconds;
            var exceptionCount = scenarioResult == null || scenarioResult.exceptionCount <= 0
                ? forensicDirectoryCount
                : scenarioResult.exceptionCount;
            var warningCount = scenarioResult == null ? 0 : scenarioResult.warningCount;
            return new RunArchiveMeta(
                scenarioName,
                verdict,
                startedAtText,
                finishedAtText,
                durationSeconds,
                captureCount,
                auditCount,
                auditFindingCount,
                exceptionCount,
                warningCount,
                droppedFrameCount,
                recordingNames,
                visualRegressionSummary,
                performanceSummary,
                ResolveGitCommitHash(),
                Application.unityVersion);
        }

        private static void SaveScenarioResult(string runDirectoryPath, RunArchiveScenarioResult scenarioResult)
        {
            if (scenarioResult == null)
            {
                return;
            }

            var outputFilePath = Path.Combine(runDirectoryPath, ScenarioResultFileName);
            File.WriteAllText(outputFilePath, JsonUtility.ToJson(scenarioResult, true));
        }

        private static RunArchiveScenarioResult RewriteScenarioResult(
            RunArchiveScenarioResult scenarioResult,
            Dictionary<string, string> evidenceCaptureMap,
            Dictionary<string, string> evidenceSnapshotMap,
            string[] recordingNames,
            string[] forensicDirectoryNames)
        {
            if (scenarioResult == null)
            {
                return null;
            }

            if (scenarioResult.steps != null)
            {
                for (var stepIndex = 0; stepIndex < scenarioResult.steps.Length; stepIndex++)
                {
                    var step = scenarioResult.steps[stepIndex];
                    if (step == null || step.evidence == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(step.evidence.capture) && evidenceCaptureMap.TryGetValue(step.evidence.capture, out var relativeCapturePath))
                    {
                        step.evidence.capture = relativeCapturePath;
                    }

                    if (!string.IsNullOrEmpty(step.evidence.snapshot) && evidenceSnapshotMap.TryGetValue(step.evidence.snapshot, out var relativeSnapshotPath))
                    {
                        step.evidence.snapshot = relativeSnapshotPath;
                    }
                }
            }

            scenarioResult.recordings = CreateRelativeRecordingPaths(recordingNames);
            scenarioResult.exceptions = CreateRelativeDirectoryPaths(ForensicsDirectoryName, forensicDirectoryNames);
            return scenarioResult;
        }

        private static string[] CreateRelativeRecordingPaths(string[] recordingNames)
        {
            if (recordingNames == null || recordingNames.Length == 0)
            {
                return Array.Empty<string>();
            }

            var relativePaths = new string[recordingNames.Length];
            for (var recordingIndex = 0; recordingIndex < recordingNames.Length; recordingIndex++)
            {
                relativePaths[recordingIndex] = NormalizePathForJson(Path.Combine(RecordingsDirectoryName, recordingNames[recordingIndex]));
            }

            return relativePaths;
        }

        private static string[] CreateRelativeDirectoryPaths(string parentDirectoryName, string[] directoryNames)
        {
            if (directoryNames == null || directoryNames.Length == 0)
            {
                return Array.Empty<string>();
            }

            var relativePaths = new string[directoryNames.Length];
            for (var directoryIndex = 0; directoryIndex < directoryNames.Length; directoryIndex++)
            {
                relativePaths[directoryIndex] = NormalizePathForJson(Path.Combine(parentDirectoryName, directoryNames[directoryIndex]));
            }

            return relativePaths;
        }

        private static int CopyScenarioCaptures(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var sourceDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, DefaultScenarioCaptureDirectoryName);
            if (!Directory.Exists(sourceDirectoryPath))
            {
                return 0;
            }

            var copiedFileCount = 0;
            var sourceFilePaths = Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(sourceFilePaths, StringComparer.Ordinal);
            for (var fileIndex = 0; fileIndex < sourceFilePaths.Length; fileIndex++)
            {
                var sourceFilePath = sourceFilePaths[fileIndex];
                var fileName = Path.GetFileName(sourceFilePath);
                var isCaptureFile = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
                var isAuditFile = fileName.EndsWith(AuditFileNameSuffix, StringComparison.OrdinalIgnoreCase);
                if (!isCaptureFile && !isAuditFile)
                {
                    continue;
                }

                if (!IsArtifactInWindow(sourceFilePath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                var destinationFilePath = Path.Combine(outputDirectoryPath, fileName);
                File.Copy(sourceFilePath, destinationFilePath, true);
                copiedFileCount++;
            }

            return copiedFileCount;
        }

        private static void CopyScenarioEvidenceFiles(
            RunArchiveScenarioResult scenarioResult,
            string captureOutputDirectoryPath,
            string snapshotOutputDirectoryPath,
            Dictionary<string, string> evidenceCaptureMap,
            Dictionary<string, string> evidenceSnapshotMap)
        {
            if (scenarioResult == null || scenarioResult.steps == null)
            {
                return;
            }

            for (var stepIndex = 0; stepIndex < scenarioResult.steps.Length; stepIndex++)
            {
                var step = scenarioResult.steps[stepIndex];
                if (step == null || step.evidence == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(step.evidence.capture) && File.Exists(step.evidence.capture))
                {
                    var destinationFilePath = Path.Combine(captureOutputDirectoryPath, Path.GetFileName(step.evidence.capture));
                    File.Copy(step.evidence.capture, destinationFilePath, true);
                    evidenceCaptureMap[step.evidence.capture] = NormalizePathForJson(Path.Combine(CapturesDirectoryName, Path.GetFileName(destinationFilePath)));
                }

                if (string.IsNullOrEmpty(step.evidence.snapshot) || !File.Exists(step.evidence.snapshot))
                {
                    continue;
                }

                var snapshotFileName = Path.GetFileName(step.evidence.snapshot);
                var destinationSnapshotPath = Path.Combine(snapshotOutputDirectoryPath, snapshotFileName);
                File.Copy(step.evidence.snapshot, destinationSnapshotPath, true);
                WriteSnapshotCompactText(destinationSnapshotPath);
                evidenceSnapshotMap[step.evidence.snapshot] = NormalizePathForJson(Path.Combine(SnapshotsDirectoryName, snapshotFileName));
            }
        }

        private static void CopyWindowSnapshots(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string snapshotOutputDirectoryPath, Dictionary<string, string> evidenceSnapshotMap)
        {
            var sourceDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, SnapshotSourceDirectoryName);
            if (!Directory.Exists(sourceDirectoryPath))
            {
                return;
            }

            var snapshotFilePaths = Directory.GetFiles(sourceDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(snapshotFilePaths, StringComparer.Ordinal);
            for (var fileIndex = 0; fileIndex < snapshotFilePaths.Length; fileIndex++)
            {
                var sourceFilePath = snapshotFilePaths[fileIndex];
                if (!IsArtifactInWindow(sourceFilePath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                if (evidenceSnapshotMap.ContainsKey(sourceFilePath))
                {
                    continue;
                }

                var destinationFilePath = Path.Combine(snapshotOutputDirectoryPath, Path.GetFileName(sourceFilePath));
                File.Copy(sourceFilePath, destinationFilePath, true);
                WriteSnapshotCompactText(destinationFilePath);
                evidenceSnapshotMap[sourceFilePath] = NormalizePathForJson(Path.Combine(SnapshotsDirectoryName, Path.GetFileName(destinationFilePath)));
            }
        }

        private static string[] CopyRecordings(RunArchiveScenarioResult scenarioResult, DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var sourceDirectoryPaths = new List<string>();
            if (scenarioResult != null && scenarioResult.recordings != null && scenarioResult.recordings.Length > 0)
            {
                for (var recordingIndex = 0; recordingIndex < scenarioResult.recordings.Length; recordingIndex++)
                {
                    var sourceDirectoryPath = scenarioResult.recordings[recordingIndex];
                    if (string.IsNullOrEmpty(sourceDirectoryPath) || !Directory.Exists(sourceDirectoryPath))
                    {
                        continue;
                    }

                    sourceDirectoryPaths.Add(sourceDirectoryPath);
                }
            }
            else
            {
                var recordingRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, RecordingSourceDirectoryName);
                if (Directory.Exists(recordingRootDirectoryPath))
                {
                    var candidateDirectoryPaths = Directory.GetDirectories(recordingRootDirectoryPath, "*", SearchOption.TopDirectoryOnly);
                    Array.Sort(candidateDirectoryPaths, StringComparer.Ordinal);
                    for (var directoryIndex = 0; directoryIndex < candidateDirectoryPaths.Length; directoryIndex++)
                    {
                        var candidateDirectoryPath = candidateDirectoryPaths[directoryIndex];
                        var directoryName = Path.GetFileName(candidateDirectoryPath);
                        if (directoryName == "_current")
                        {
                            continue;
                        }

                        if (!IsArtifactInWindow(candidateDirectoryPath, runStartedAt, runFinishedAt))
                        {
                            continue;
                        }

                        sourceDirectoryPaths.Add(candidateDirectoryPath);
                    }
                }
            }

            var copiedRecordingNames = new List<string>(sourceDirectoryPaths.Count);
            for (var sourceIndex = 0; sourceIndex < sourceDirectoryPaths.Count; sourceIndex++)
            {
                var sourceDirectoryPath = sourceDirectoryPaths[sourceIndex];
                var directoryName = Path.GetFileName(sourceDirectoryPath);
                var destinationDirectoryPath = Path.Combine(outputDirectoryPath, directoryName);
                CopyDirectory(sourceDirectoryPath, destinationDirectoryPath);
                RewriteRecordingManifest(destinationDirectoryPath, directoryName);
                copiedRecordingNames.Add(directoryName);
            }

            return copiedRecordingNames.ToArray();
        }

        private static string[] CopyForensics(RunArchiveScenarioResult scenarioResult, DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var sourceDirectoryPaths = new List<string>();
            if (scenarioResult != null && scenarioResult.exceptions != null && scenarioResult.exceptions.Length > 0)
            {
                for (var forensicIndex = 0; forensicIndex < scenarioResult.exceptions.Length; forensicIndex++)
                {
                    var sourceDirectoryPath = scenarioResult.exceptions[forensicIndex];
                    if (string.IsNullOrEmpty(sourceDirectoryPath) || !Directory.Exists(sourceDirectoryPath))
                    {
                        continue;
                    }

                    sourceDirectoryPaths.Add(sourceDirectoryPath);
                }
            }
            else
            {
                var forensicRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, ForensicsSourceDirectoryName);
                if (Directory.Exists(forensicRootDirectoryPath))
                {
                    var candidateDirectoryPaths = Directory.GetDirectories(forensicRootDirectoryPath, "*", SearchOption.TopDirectoryOnly);
                    Array.Sort(candidateDirectoryPaths, StringComparer.Ordinal);
                    for (var directoryIndex = 0; directoryIndex < candidateDirectoryPaths.Length; directoryIndex++)
                    {
                        var candidateDirectoryPath = candidateDirectoryPaths[directoryIndex];
                        if (!IsArtifactInWindow(candidateDirectoryPath, runStartedAt, runFinishedAt))
                        {
                            continue;
                        }

                        sourceDirectoryPaths.Add(candidateDirectoryPath);
                    }
                }
            }

            var copiedDirectoryNames = new List<string>(sourceDirectoryPaths.Count);
            for (var sourceIndex = 0; sourceIndex < sourceDirectoryPaths.Count; sourceIndex++)
            {
                var sourceDirectoryPath = sourceDirectoryPaths[sourceIndex];
                var directoryName = Path.GetFileName(sourceDirectoryPath);
                var destinationDirectoryPath = Path.Combine(outputDirectoryPath, directoryName);
                CopyDirectory(sourceDirectoryPath, destinationDirectoryPath);
                copiedDirectoryNames.Add(directoryName);
            }

            return copiedDirectoryNames.ToArray();
        }

        private static void CopyMonkeyRuns(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var monkeyRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, MonkeySourceDirectoryName);
            if (!Directory.Exists(monkeyRootDirectoryPath))
            {
                return;
            }

            Directory.CreateDirectory(outputDirectoryPath);
            var candidateDirectoryPaths = Directory.GetDirectories(monkeyRootDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateDirectoryPaths, StringComparer.Ordinal);
            for (var directoryIndex = 0; directoryIndex < candidateDirectoryPaths.Length; directoryIndex++)
            {
                var candidateDirectoryPath = candidateDirectoryPaths[directoryIndex];
                if (!IsArtifactInWindow(candidateDirectoryPath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                var destinationDirectoryPath = Path.Combine(outputDirectoryPath, Path.GetFileName(candidateDirectoryPath));
                CopyDirectory(candidateDirectoryPath, destinationDirectoryPath);
            }
        }

        private static RunArchiveVisualRegressionSummary CopyVisualRegression(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var visualRegressionRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, VisualRegressionSourceDirectoryName);
            if (!Directory.Exists(visualRegressionRootDirectoryPath))
            {
                return new RunArchiveVisualRegressionSummary(0, 0, 0);
            }

            var candidateDirectoryPaths = Directory.GetDirectories(visualRegressionRootDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateDirectoryPaths, StringComparer.Ordinal);
            for (var directoryIndex = candidateDirectoryPaths.Length - 1; directoryIndex >= 0; directoryIndex--)
            {
                var candidateDirectoryPath = candidateDirectoryPaths[directoryIndex];
                if (!IsArtifactInWindow(candidateDirectoryPath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                var reportFilePath = Path.Combine(candidateDirectoryPath, ReportFileName);
                if (!File.Exists(reportFilePath))
                {
                    continue;
                }

                CopyDirectory(candidateDirectoryPath, outputDirectoryPath);
                var reportJson = File.ReadAllText(reportFilePath);
                var report = JsonUtility.FromJson<VisualRegressionReport>(reportJson);
                if (report == null)
                {
                    return new RunArchiveVisualRegressionSummary(0, 0, 0);
                }

                RewriteVisualRegressionReport(report, outputDirectoryPath);
                return new RunArchiveVisualRegressionSummary(report.passCount, report.failCount, report.noBaselineCount);
            }

            return new RunArchiveVisualRegressionSummary(0, 0, 0);
        }

        private static RunArchivePerformanceSummary CopyPerformance(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputFilePath)
        {
            var performanceDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, PerformanceSourceDirectoryName);
            if (!Directory.Exists(performanceDirectoryPath))
            {
                return new RunArchivePerformanceSummary(0.0f);
            }

            var candidateFilePaths = Directory.GetFiles(performanceDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateFilePaths, StringComparer.Ordinal);
            for (var fileIndex = candidateFilePaths.Length - 1; fileIndex >= 0; fileIndex--)
            {
                var candidateFilePath = candidateFilePaths[fileIndex];
                if (!IsArtifactInWindow(candidateFilePath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                File.Copy(candidateFilePath, outputFilePath, true);
                var reportJson = File.ReadAllText(candidateFilePath);
                var report = JsonUtility.FromJson<PerformanceReport>(reportJson);
                var percentile95 = report == null || report.summary == null ? 0.0f : report.summary.frameMsP95;
                return new RunArchivePerformanceSummary(percentile95);
            }

            return new RunArchivePerformanceSummary(0.0f);
        }

        private static void CopyPlayerLog(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputFilePath)
        {
            var debugOutputDirectoryPath = DebugOutputPath.DirectoryPath;
            if (!Directory.Exists(debugOutputDirectoryPath))
            {
                return;
            }

            var candidateFilePaths = Directory.GetFiles(debugOutputDirectoryPath, $"{PlayerLogFileNamePrefix}*.log", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateFilePaths, StringComparer.Ordinal);
            for (var fileIndex = candidateFilePaths.Length - 1; fileIndex >= 0; fileIndex--)
            {
                var candidateFilePath = candidateFilePaths[fileIndex];
                if (!IsArtifactInWindow(candidateFilePath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                File.Copy(candidateFilePath, outputFilePath, true);
                return;
            }
        }

        private static int CountDroppedFrames(string recordingsDirectoryPath)
        {
            if (!Directory.Exists(recordingsDirectoryPath))
            {
                return 0;
            }

            var manifestFilePaths = Directory.GetFiles(recordingsDirectoryPath, VideoRecorder.ManifestFileName, SearchOption.AllDirectories);
            var droppedFrameCount = 0;
            for (var fileIndex = 0; fileIndex < manifestFilePaths.Length; fileIndex++)
            {
                var manifestJson = File.ReadAllText(manifestFilePaths[fileIndex]);
                var manifest = JsonUtility.FromJson<VideoRecordingManifest>(manifestJson);
                if (manifest == null)
                {
                    continue;
                }

                droppedFrameCount += manifest.droppedFrameCount;
            }

            return droppedFrameCount;
        }

        private static void RewriteRecordingManifest(string recordingDirectoryPath, string recordingName)
        {
            var manifestFilePath = Path.Combine(recordingDirectoryPath, VideoRecorder.ManifestFileName);
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

            manifest.name = string.IsNullOrEmpty(manifest.name) ? recordingName : manifest.name;
            manifest.ffmpegCommand = VideoRecorder.CreateFfmpegCommand(
                manifest.framesPerSecond,
                recordingDirectoryPath,
                manifest.name,
                manifest.durationSeconds,
                manifest.hasAudio);
            File.WriteAllText(manifestFilePath, JsonUtility.ToJson(manifest, true));
        }

        private static void RewriteVisualRegressionReport(VisualRegressionReport report, string outputDirectoryPath)
        {
            report.outputDirectory = NormalizePathForJson(VisualRegressionDirectoryName);
            report.capturesDirectory = NormalizePathForJson(CapturesDirectoryName);
            if (report.results != null)
            {
                for (var resultIndex = 0; resultIndex < report.results.Length; resultIndex++)
                {
                    var result = report.results[resultIndex];
                    if (result == null)
                    {
                        continue;
                    }

                    result.actualPath = CopyVisualRegressionAsset(result.actualPath, outputDirectoryPath, $"{result.capture}-actual{CaptureExtension}");
                    result.diffPath = CopyVisualRegressionAsset(result.diffPath, outputDirectoryPath, $"{result.capture}-diff{CaptureExtension}");
                    result.baselinePath = CopyVisualRegressionAsset(result.baselinePath, outputDirectoryPath, $"{result.capture}-baseline{CaptureExtension}");
                }
            }

            var outputReportPath = Path.Combine(outputDirectoryPath, ReportFileName);
            File.WriteAllText(outputReportPath, JsonUtility.ToJson(report, true));
        }

        private static string CopyVisualRegressionAsset(string sourceFilePath, string outputDirectoryPath, string preferredFileName)
        {
            if (string.IsNullOrEmpty(sourceFilePath))
            {
                return string.Empty;
            }

            var sourceExists = File.Exists(sourceFilePath);
            var destinationFileName = string.IsNullOrEmpty(preferredFileName) ? Path.GetFileName(sourceFilePath) : preferredFileName;
            var destinationFilePath = Path.Combine(outputDirectoryPath, destinationFileName);
            if (sourceExists && !Path.GetFullPath(sourceFilePath).Equals(Path.GetFullPath(destinationFilePath), StringComparison.Ordinal))
            {
                File.Copy(sourceFilePath, destinationFilePath, true);
            }

            if (!File.Exists(destinationFilePath))
            {
                return string.Empty;
            }

            return NormalizePathForJson(Path.Combine(VisualRegressionDirectoryName, destinationFileName));
        }

        private static int CountAuditFindings(string captureDirectoryPath)
        {
            if (!Directory.Exists(captureDirectoryPath))
            {
                return 0;
            }

            var auditFilePaths = Directory.GetFiles(captureDirectoryPath, $"*{AuditFileNameSuffix}", SearchOption.TopDirectoryOnly);
            var findingCount = 0;
            for (var fileIndex = 0; fileIndex < auditFilePaths.Length; fileIndex++)
            {
                var reportJson = File.ReadAllText(auditFilePaths[fileIndex]);
                var report = JsonUtility.FromJson<UiLayoutAuditReport>(reportJson);
                if (report == null || report.entries == null)
                {
                    continue;
                }

                findingCount += report.entries.Length;
            }

            return findingCount;
        }

        private static int CountFiles(string directoryPath, string searchPattern)
        {
            if (!Directory.Exists(directoryPath))
            {
                return 0;
            }

            return Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly).Length;
        }

        private static void WriteSnapshotCompactText(string snapshotFilePath)
        {
            var snapshotJson = File.ReadAllText(snapshotFilePath);
            var snapshot = JsonUtility.FromJson<UiSnapshotDocument>(snapshotJson);
            if (snapshot == null)
            {
                return;
            }

            var compactText = UiSnapshot.ToCompactText(snapshot);
            var compactTextPath = Path.ChangeExtension(snapshotFilePath, SnapshotCompactTextExtension);
            File.WriteAllText(compactTextPath, compactText);
        }

        private static RunArchiveScenarioResult LoadScenarioResult(string scenarioResultPath)
        {
            if (string.IsNullOrEmpty(scenarioResultPath) || !File.Exists(scenarioResultPath))
            {
                return null;
            }

            var scenarioResultJson = File.ReadAllText(scenarioResultPath);
            return JsonUtility.FromJson<RunArchiveScenarioResult>(scenarioResultJson);
        }

        private static bool IsArtifactInWindow(string path, DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt)
        {
            DateTimeOffset lastWriteTime;
            if (File.Exists(path))
            {
                lastWriteTime = File.GetLastWriteTime(path);
            }
            else if (Directory.Exists(path))
            {
                lastWriteTime = Directory.GetLastWriteTime(path);
            }
            else
            {
                return false;
            }

            var paddedStart = runStartedAt.AddSeconds(-ArtifactWindowPaddingSeconds);
            var paddedEnd = runFinishedAt.AddSeconds(ArtifactWindowPaddingSeconds);
            return lastWriteTime >= paddedStart && lastWriteTime <= paddedEnd;
        }

        private static string CreateUniqueRunDirectory(string verificationRunsDirectoryPath, DateTimeOffset runStartedAt)
        {
            var runDirectoryName = $"{RunDirectoryPrefix}{runStartedAt.ToString(TimestampFormat, CultureInfo.InvariantCulture)}";
            var runDirectoryPath = Path.Combine(verificationRunsDirectoryPath, runDirectoryName);
            if (!Directory.Exists(runDirectoryPath))
            {
                Directory.CreateDirectory(runDirectoryPath);
                return runDirectoryPath;
            }

            for (var suffixIndex = 1; suffixIndex < 1000; suffixIndex++)
            {
                var suffixedDirectoryPath = Path.Combine(verificationRunsDirectoryPath, $"{runDirectoryName}-{suffixIndex:D2}");
                if (Directory.Exists(suffixedDirectoryPath))
                {
                    continue;
                }

                Directory.CreateDirectory(suffixedDirectoryPath);
                return suffixedDirectoryPath;
            }

            throw new IOException("RunArchive の出力先を確保できません。");
        }

        private static DateTimeOffset ResolveAnchorTime(string scenarioResultPath, RunArchiveScenarioResult scenarioResult)
        {
            if (TryParseDateTimeOffset(scenarioResult == null ? string.Empty : scenarioResult.finishedAt, out var finishedAt))
            {
                return finishedAt;
            }

            if (TryParseDateTimeOffset(scenarioResult == null ? string.Empty : scenarioResult.startedAt, out var startedAt))
            {
                return startedAt;
            }

            if (!string.IsNullOrEmpty(scenarioResultPath) && File.Exists(scenarioResultPath))
            {
                return File.GetLastWriteTime(scenarioResultPath);
            }

            return DateTimeOffset.Now;
        }

        private static DateTimeOffset ResolveStartedAt(DateTimeOffset anchorTime, RunArchiveScenarioResult scenarioResult)
        {
            if (TryParseDateTimeOffset(scenarioResult == null ? string.Empty : scenarioResult.startedAt, out var startedAt))
            {
                return startedAt;
            }

            if (scenarioResult != null && scenarioResult.durationSeconds > 0.0f)
            {
                return anchorTime.AddSeconds(-scenarioResult.durationSeconds);
            }

            return anchorTime;
        }

        private static DateTimeOffset ResolveFinishedAt(DateTimeOffset anchorTime, RunArchiveScenarioResult scenarioResult, DateTimeOffset runStartedAt)
        {
            if (TryParseDateTimeOffset(scenarioResult == null ? string.Empty : scenarioResult.finishedAt, out var finishedAt))
            {
                return finishedAt;
            }

            if (scenarioResult != null && scenarioResult.durationSeconds > 0.0f)
            {
                return runStartedAt.AddSeconds(scenarioResult.durationSeconds);
            }

            return anchorTime;
        }

        private static bool TryParseDateTimeOffset(string text, out DateTimeOffset value)
        {
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value))
            {
                return true;
            }

            if (DateTimeOffset.TryParseExact(text, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value))
            {
                return true;
            }

            return DateTimeOffset.TryParseExact(text, TimestampWithMillisecondsFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value);
        }

        private static string FindLatestFile(string directoryPath, string searchPattern)
        {
            if (!Directory.Exists(directoryPath))
            {
                return string.Empty;
            }

            var candidateFilePaths = Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly);
            var latestFilePath = string.Empty;
            var latestWriteTime = DateTime.MinValue;
            for (var fileIndex = 0; fileIndex < candidateFilePaths.Length; fileIndex++)
            {
                var candidateFilePath = candidateFilePaths[fileIndex];
                var lastWriteTime = File.GetLastWriteTime(candidateFilePath);
                if (lastWriteTime <= latestWriteTime)
                {
                    continue;
                }

                latestWriteTime = lastWriteTime;
                latestFilePath = candidateFilePath;
            }

            return latestFilePath;
        }

        private static void CopyDirectory(string sourceDirectoryPath, string destinationDirectoryPath)
        {
            Directory.CreateDirectory(destinationDirectoryPath);
            var sourceFilePaths = Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            for (var fileIndex = 0; fileIndex < sourceFilePaths.Length; fileIndex++)
            {
                var sourceFilePath = sourceFilePaths[fileIndex];
                var destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(sourceFilePath));
                File.Copy(sourceFilePath, destinationFilePath, true);
            }

            var childDirectoryPaths = Directory.GetDirectories(sourceDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            for (var directoryIndex = 0; directoryIndex < childDirectoryPaths.Length; directoryIndex++)
            {
                var childSourceDirectoryPath = childDirectoryPaths[directoryIndex];
                var childDestinationDirectoryPath = Path.Combine(destinationDirectoryPath, Path.GetFileName(childSourceDirectoryPath));
                CopyDirectory(childSourceDirectoryPath, childDestinationDirectoryPath);
            }
        }

        private static string ResolveGitCommitHash()
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = GitExecutableName,
                        Arguments = $"rev-parse --short HEAD",
                        WorkingDirectory = GetProjectRootDirectoryPath(),
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    };

                    if (!process.Start())
                    {
                        return string.Empty;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);
                    if (process.ExitCode != 0)
                    {
                        return string.Empty;
                    }

                    return string.IsNullOrWhiteSpace(output) ? string.Empty : output.Trim();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetProjectRootDirectoryPath()
        {
            var assetsDirectoryPath = Application.dataPath;
            return Path.GetDirectoryName(assetsDirectoryPath) ?? assetsDirectoryPath;
        }

        private static string NormalizePathForJson(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
