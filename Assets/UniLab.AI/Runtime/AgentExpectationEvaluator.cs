#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// 目標判定を 02 の expect 語彙へ寄せ、LLM の成功自己申告を混ぜないための評価器です。
    /// </summary>
    public sealed class AgentExpectationEvaluator
    {
        private readonly List<ScenarioExpectationFailure> _failures = new List<ScenarioExpectationFailure>();

        /// <summary>
        /// 最新の失敗理由を返し、外側が未達理由をプロンプトへ戻せるようにします。
        /// </summary>
        public IReadOnlyList<ScenarioExpectationFailure> Failures
        {
            get { return _failures; }
        }

        /// <summary>
        /// すべての期待値を観測だけで評価し、成功判定を UI と game 状態へ固定します。
        /// </summary>
        public bool Evaluate(ScenarioExpectation[] expectations, UiSnapshotDocument snapshot, UiSnapshotDiff diff)
        {
            _failures.Clear();
            if (expectations == null || expectations.Length == 0)
            {
                return true;
            }

            for (var expectationIndex = 0; expectationIndex < expectations.Length; expectationIndex++)
            {
                EvaluateExpectation(expectations[expectationIndex], snapshot, diff);
            }

            return _failures.Count == 0;
        }

        private void EvaluateExpectation(ScenarioExpectation expectation, UiSnapshotDocument snapshot, UiSnapshotDiff diff)
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
                case "gameState": ExpectGameState(expectation, snapshot); return;
                case "changed": ExpectChanged(expectation, diff); return;
                case "noException": return;
                case "auditClean": return;
                case "noDroppedFrames": return;
                case "frameMsP95Below": return;
                case "gcAllocBelow": return;
                case "noGcCollection": return;
                default: AddFailure(expectation.kind, expectation.target, expectation.value, "未対応の expect kind です。"); return;
            }
        }

        private void ExpectText(ScenarioExpectation expectation, UiSnapshotDocument snapshot, bool shouldExist)
        {
            var found = HasText(snapshot, expectation.value, expectation.scope);
            if (found == shouldExist)
            {
                return;
            }

            AddFailure(expectation.kind, expectation.scope, expectation.value, shouldExist ? "指定テキストが見つかりません。" : "指定テキストが表示されています。");
        }

        private void ExpectElement(ScenarioExpectation expectation, UiSnapshotDocument snapshot, bool shouldExist, bool shouldBeInteractable)
        {
            var element = FindElement(snapshot, expectation.target);
            if (!shouldExist)
            {
                if (element != null)
                {
                    AddFailure(expectation.kind, expectation.target, string.Empty, "要素が存在しています。");
                }

                return;
            }

            if (element == null)
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素が見つかりません。");
                return;
            }

            if (shouldBeInteractable && (!element.interactable || !string.IsNullOrEmpty(element.blockedBy)))
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素が操作可能ではありません。");
            }
        }

        private void ExpectDisabled(ScenarioExpectation expectation, UiSnapshotDocument snapshot)
        {
            var element = FindElement(snapshot, expectation.target);
            if (element == null)
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素が見つかりません。");
                return;
            }

            if (element.interactable && string.IsNullOrEmpty(element.blockedBy))
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素が操作可能です。");
            }
        }

        private void ExpectFocused(ScenarioExpectation expectation, UiSnapshotDocument snapshot)
        {
            var element = FindElement(snapshot, expectation.target);
            if (element == null || !element.focused)
            {
                AddFailure(expectation.kind, expectation.target, string.Empty, "要素にフォーカスがありません。");
            }
        }

        private void ExpectScene(ScenarioExpectation expectation, UiSnapshotDocument snapshot)
        {
            var actualScene = snapshot == null ? string.Empty : snapshot.activeScene;
            if (!string.Equals(actualScene, expectation.value, StringComparison.Ordinal))
            {
                AddFailure(expectation.kind, string.Empty, expectation.value, $"シーンが一致しません。 actual={actualScene}");
            }
        }

        private void ExpectGameState(ScenarioExpectation expectation, UiSnapshotDocument snapshot)
        {
            var actual = FindGameValue(snapshot, expectation.key);
            if (actual != null && CompareGameState(actual, expectation.op, expectation.value))
            {
                return;
            }

            AddFailure(expectation.kind, expectation.key, expectation.value, $"gameState が一致しません。 actual={actual ?? string.Empty}");
        }

        private void ExpectChanged(ScenarioExpectation expectation, UiSnapshotDiff diff)
        {
            if (diff == null || diff.isEmpty)
            {
                AddFailure(expectation.kind, string.Empty, string.Empty, "スナップショット差分が空です。");
            }
        }

        private void AddFailure(string kind, string target, string value, string message)
        {
            _failures.Add(new ScenarioExpectationFailure
            {
                kind = kind ?? string.Empty,
                target = target ?? string.Empty,
                value = value ?? string.Empty,
                message = message ?? string.Empty,
                evidencePath = string.Empty,
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
            var resolvedOperation = string.IsNullOrEmpty(operation) ? "eq" : operation;
            switch (resolvedOperation)
            {
                case "eq": return string.Equals(actual, expected, StringComparison.Ordinal);
                case "ne": return !string.Equals(actual, expected, StringComparison.Ordinal);
                case "contains": return !string.IsNullOrEmpty(actual) && actual.Contains(expected ?? string.Empty);
                case "lt":
                case "le":
                case "gt":
                case "ge":
                    return CompareNumber(actual, resolvedOperation, expected);
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

        private static bool EndsWithPath(string path, string target)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(target))
            {
                return false;
            }

            return path.EndsWith($"/{target}", StringComparison.Ordinal);
        }
    }
}
#endif
