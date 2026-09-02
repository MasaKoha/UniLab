#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;

namespace UniLab.AI
{
    /// <summary>
    /// UI スナップショット・差分・計測値を読み、シナリオ期待値の失敗一覧を生成します。
    /// </summary>
    internal sealed class ScenarioExpectationEvaluator
    {
        private readonly List<ScenarioExpectationFailure> _failures = new List<ScenarioExpectationFailure>();

        internal IReadOnlyList<ScenarioExpectationFailure> EvaluateExpectations(UiScenarioStep step, UiSnapshotDocument snapshot, UiSnapshotDiff diff, int forensicsStartCount, ExceptionForensics forensics, VideoRecorder videoRecorder, PerformanceRecorder performanceRecorder)
        {
            _failures.Clear();
            var expectations = step.expect ?? Array.Empty<ScenarioExpectation>();
            for (var expectationIndex = 0; expectationIndex < expectations.Length; expectationIndex++)
            {
                EvaluateExpectation(expectations[expectationIndex], snapshot, diff, forensicsStartCount, forensics, videoRecorder, performanceRecorder);
            }

            return _failures;
        }

        private void EvaluateExpectation(ScenarioExpectation expectation, UiSnapshotDocument snapshot, UiSnapshotDiff diff, int forensicsStartCount, ExceptionForensics forensics, VideoRecorder videoRecorder, PerformanceRecorder performanceRecorder)
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
                case "noException": ExpectNoException(expectation, forensicsStartCount, forensics); return;
                case "auditClean": ExpectAuditClean(expectation); return;
                case "gameState": ExpectGameState(expectation, snapshot); return;
                case "changed": ExpectChanged(expectation, diff); return;
                case "noDroppedFrames": ExpectNoDroppedFrames(expectation, videoRecorder); return;
                case "frameMsP95Below": ExpectFrameMilliseconds(expectation, performanceRecorder); return;
                case "gcAllocBelow": ExpectGarbageCollectionAlloc(expectation, performanceRecorder); return;
                case "noGcCollection": ExpectNoGarbageCollection(expectation, performanceRecorder); return;
                default: AddFailure(expectation.kind, expectation.target, expectation.value, "未対応の expect kind です。", string.Empty); return;
            }
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

        private void ExpectNoException(ScenarioExpectation expectation, int forensicsStartCount, ExceptionForensics forensics)
        {
            var capturedCount = forensics == null ? 0 : forensics.CapturedCount;
            if (capturedCount <= forensicsStartCount)
            {
                return;
            }

            var directories = forensics.CapturedDirectories;
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

        private void ExpectNoDroppedFrames(ScenarioExpectation expectation, VideoRecorder videoRecorder)
        {
            var droppedFrameCount = videoRecorder == null ? 0 : videoRecorder.DroppedFrameCount;
            if (droppedFrameCount != 0)
            {
                AddFailure(expectation.kind, string.Empty, droppedFrameCount.ToString(CultureInfo.InvariantCulture), "録画中に捨てたフレームがあります。", string.Empty);
            }
        }

        private void ExpectFrameMilliseconds(ScenarioExpectation expectation, PerformanceRecorder performanceRecorder)
        {
            var report = performanceRecorder == null ? null : performanceRecorder.CaptureCurrentStepReport();
            if (report == null)
            {
                return;
            }

            if (!float.TryParse(expectation.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) || report.frameMsP95 >= threshold)
            {
                AddFailure(expectation.kind, string.Empty, expectation.value, $"p95 フレーム時間がしきい値以上です。 actual={report.frameMsP95:F2}", string.Empty);
            }
        }

        private void ExpectGarbageCollectionAlloc(ScenarioExpectation expectation, PerformanceRecorder performanceRecorder)
        {
            var report = performanceRecorder == null ? null : performanceRecorder.CaptureCurrentStepReport();
            if (report == null)
            {
                return;
            }

            if (!long.TryParse(expectation.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold) || report.gcAllocBytes >= threshold)
            {
                AddFailure(expectation.kind, string.Empty, expectation.value, $"GC 割り当てがしきい値以上です。 actual={report.gcAllocBytes}", string.Empty);
            }
        }

        private void ExpectNoGarbageCollection(ScenarioExpectation expectation, PerformanceRecorder performanceRecorder)
        {
            var report = performanceRecorder == null ? null : performanceRecorder.CaptureCurrentStepReport();
            if (report != null && report.gcCollections != 0)
            {
                AddFailure(expectation.kind, string.Empty, report.gcCollections.ToString(CultureInfo.InvariantCulture), "このステップ中に GC が走りました。", string.Empty);
            }
        }

        private void AddFailure(string kind, string target, string value, string message, string evidencePath)
        {
            _failures.Add(new ScenarioExpectationFailure
            {
                kind = kind ?? string.Empty,
                target = target ?? string.Empty,
                value = value ?? string.Empty,
                message = message ?? string.Empty,
                evidencePath = evidencePath ?? string.Empty,
            });
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

                if (string.Equals(element.path, target, StringComparison.Ordinal) || UiScenarioStepReader.EndsWithPath(element.path, target) || string.Equals(element.name, target, StringComparison.Ordinal))
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
            var normalizedOperation = NormalizeComparisonOperation(operation);
            switch (normalizedOperation)
            {
                case "eq": return string.Equals(actual, expected, StringComparison.Ordinal);
                case "ne": return !string.Equals(actual, expected, StringComparison.Ordinal);
                case "contains": return !string.IsNullOrEmpty(actual) && actual.Contains(expected ?? string.Empty);
                case "lt":
                case "le":
                case "gt":
                case "ge":
                    return CompareNumber(actual, normalizedOperation, expected);
                default:
                    return false;
            }
        }

        private static bool CompareNumber(string actual, string operation, string expected)
        {
            var normalizedOperation = NormalizeComparisonOperation(operation);
            var actualIsNumber = double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var actualNumber);
            var expectedIsNumber = double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedNumber);
            if (!actualIsNumber || !expectedIsNumber)
            {
                switch (normalizedOperation)
                {
                    case "eq": return string.Equals(actual, expected, StringComparison.Ordinal);
                    case "ne": return !string.Equals(actual, expected, StringComparison.Ordinal);
                    default: return false;
                }
            }

            switch (normalizedOperation)
            {
                case "eq": return actualNumber.Equals(expectedNumber);
                case "ne": return !actualNumber.Equals(expectedNumber);
                case "lt": return actualNumber < expectedNumber;
                case "le": return actualNumber <= expectedNumber;
                case "gt": return actualNumber > expectedNumber;
                case "ge": return actualNumber >= expectedNumber;
                default: return false;
            }
        }

        private static string NormalizeComparisonOperation(string operation)
        {
            switch (string.IsNullOrEmpty(operation) ? "eq" : operation)
            {
                case "==":
                    return "eq";
                case "!=":
                    return "ne";
                case "<":
                    return "lt";
                case "<=":
                case "lte":
                    return "le";
                case ">":
                    return "gt";
                case ">=":
                case "gte":
                    return "ge";
                default:
                    return string.IsNullOrEmpty(operation) ? "eq" : operation;
            }
        }
    }
}
#endif
