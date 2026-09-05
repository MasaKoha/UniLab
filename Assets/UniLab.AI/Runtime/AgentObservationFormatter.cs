#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UniLab.AI
{
    /// <summary>観測と失敗理由の出力形式を一箇所に固定します。</summary>
    internal sealed class AgentObservationFormatter
    {
        private const int DefaultSettleFrames = 1;
        private const int RecommendedLabelLength = 20;

        private readonly AgentGoal _goal;
        private readonly AgentOptions _options;
        private readonly AgentExpectationEvaluator _evaluator;
        private readonly Func<bool> _isInputBusy;
        // perf: 観測ごとのバッファ割り当てを避けるため、整形呼び出し間で再利用します。
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>目標評価と入力設定を観測の文面へ反映するために受け取ります。</summary>
        internal AgentObservationFormatter(AgentGoal goal, AgentOptions options, AgentExpectationEvaluator evaluator, Func<bool> isInputBusy)
        {
            _goal = goal;
            _options = options;
            _evaluator = evaluator;
            _isInputBusy = isInputBusy;
        }

        /// <summary>現在の観測と入力候補を既存の形式で返します。</summary>
        internal string BuildFullObservation(UiSnapshotDocument snapshot, string scope = "visible")
        {
            if (!_goal.freePlay)
            {
                _evaluator.Evaluate(_goal.goal, snapshot, null);
            }
            var builder = _builder;
            builder.Clear();
            builder.AppendLine(UiSnapshot.ToCompactText(snapshot, scope));
            AppendCandidates(builder, snapshot);
            if (_isInputBusy())
            {
                builder.AppendLine("agent: inputBusy=true");
            }

            builder.Append("agent: settleFrames=");
            builder.AppendLine(ResolveSettleFrames(_options).ToString(CultureInfo.InvariantCulture));

            AppendGoalFailures(builder);
            return builder.ToString().TrimEnd();
        }

        /// <summary>観測差分と入力候補を既存の形式で返します。</summary>
        internal string BuildDiffObservation(UiSnapshotDocument before, UiSnapshotDocument after, string scope = "visible")
        {
            var diff = UiSnapshot.Compare(before, after);
            if (!_goal.freePlay)
            {
                _evaluator.Evaluate(_goal.goal, after, diff);
            }
            var visibleDiff = UiSnapshot.Compare(UiObservationScope.Filter(before, scope), UiObservationScope.Filter(after, scope));
            var diffText = FormatDiff(visibleDiff);
            var builder = _builder;
            builder.Clear();
            builder.AppendLine(diffText);
            builder.AppendLine("game:");
            AppendGameState(builder, after);
            AppendCandidates(builder, after);
            if (_isInputBusy())
            {
                builder.AppendLine("agent: inputBusy=true");
            }

            builder.Append("agent: settleFrames=");
            builder.AppendLine(ResolveSettleFrames(_options).ToString(CultureInfo.InvariantCulture));

            AppendGoalFailures(builder);
            return builder.ToString().TrimEnd();
        }

        private void AppendCandidates(StringBuilder builder, UiSnapshotDocument snapshot)
        {
            builder.AppendLine();
            builder.AppendLine("actions:");
            if (snapshot != null && snapshot.elements != null)
            {
                var targetCounts = CountActionCandidateTargets(snapshot.elements);
                for (var elementIndex = 0; elementIndex < snapshot.elements.Length; elementIndex++)
                {
                    var element = snapshot.elements[elementIndex];
                    if (!IsActionCandidate(element))
                    {
                        continue;
                    }

                    var target = string.IsNullOrEmpty(element.path) ? element.name : element.path;
                    builder.Append(" - submit/click/tap target=");
                    builder.Append(target);
                    var label = UiInputLocator.NormalizeLabelText(element.label);
                    if (targetCounts.TryGetValue(target, out var duplicateCount) && duplicateCount > 1)
                    {
                        AppendDuplicateLabelTarget(builder, label);
                    }
                    else if (!string.IsNullOrEmpty(label))
                    {
                        builder.Append(" label=");
                        builder.Append(label);
                    }

                    builder.AppendLine();
                }
            }

            AppendRawInputCandidates(builder);
        }

        private void AppendDuplicateLabelTarget(StringBuilder builder, string label)
        {
            builder.Append(" label=");
            builder.Append(label);
            var recommendedSubmit = UiInputLocator.CreateLabelTargetSpec(label, RecommendedLabelLength);
            if (!string.IsNullOrEmpty(recommendedSubmit))
            {
                builder.Append(" → submit:\"");
                builder.Append(recommendedSubmit);
                builder.Append("\"");
            }
        }

        private void AppendRawInputCandidates(StringBuilder builder)
        {
            var inputMode = string.IsNullOrEmpty(_options.inputMode) ? "gamepad" : _options.inputMode;
            if (inputMode == "gamepad" || inputMode == "all")
            {
                builder.AppendLine(" - press=south/east/north/west/start/select/leftShoulder/rightShoulder");
                builder.AppendLine(" - move=up/down/left/right");
                builder.AppendLine(" - stick=left/right x=-1..1 y=-1..1 seconds=0.1");
            }

            if (inputMode == "keyboard" || inputMode == "all")
            {
                builder.AppendLine(" - key=Enter/Escape/Space/Tab/ArrowUp/ArrowDown/ArrowLeft/ArrowRight");
                builder.AppendLine(" - text=<文字列>");
            }

            if (inputMode == "mouse" || inputMode == "all")
            {
                builder.AppendLine(" - click=<target> button=left/right/middle");
                builder.AppendLine(" - scroll=<target> amount=<数値>");
            }

            if (inputMode == "touch" || inputMode == "all")
            {
                builder.AppendLine(" - tap=<target>");
                builder.AppendLine(" - swipe from=<target> to=<target> seconds=0.1");
                builder.AppendLine(" - pinch center=<target> fromDistance=<数値> toDistance=<数値>");
            }
        }

        private void AppendGameState(StringBuilder builder, UiSnapshotDocument snapshot)
        {
            if (snapshot == null || snapshot.game == null || snapshot.game.Length == 0)
            {
                builder.AppendLine(" -");
                return;
            }

            for (var gameIndex = 0; gameIndex < snapshot.game.Length; gameIndex++)
            {
                var entry = snapshot.game[gameIndex];
                if (entry == null)
                {
                    continue;
                }

                builder.Append(" ");
                builder.Append(entry.key);
                builder.Append("=");
                builder.Append(entry.value);
            }

            builder.AppendLine();
        }

        private void AppendGoalFailures(StringBuilder builder)
        {
            if (_goal.freePlay || _evaluator.Failures.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine("goalFailures:");
            for (var failureIndex = 0; failureIndex < _evaluator.Failures.Count; failureIndex++)
            {
                var failure = _evaluator.Failures[failureIndex];
                builder.Append(" - ");
                builder.Append(failure.kind);
                builder.Append(" target=");
                builder.Append(failure.target);
                builder.Append(" value=");
                builder.Append(failure.value);
                builder.Append(" message=");
                builder.AppendLine(failure.message);
            }
        }

        /// <summary>シナリオ出力の拒否理由に目標失敗の詳細を添えます。</summary>
        internal string BuildGoalFailureSummary()
        {
            if (_goal.freePlay || _evaluator.Failures.Count == 0)
            {
                return "goalFailures: なし";
            }

            var builder = _builder;
            builder.Clear();
            builder.Append("goalFailures:");
            for (var failureIndex = 0; failureIndex < _evaluator.Failures.Count; failureIndex++)
            {
                var failure = _evaluator.Failures[failureIndex];
                builder.Append(" [");
                builder.Append(failure.kind);
                builder.Append("] target=");
                builder.Append(failure.target);
                builder.Append(" value=");
                builder.Append(failure.value);
                builder.Append(" message=");
                builder.Append(failure.message);
            }

            return builder.ToString();
        }

        /// <summary>ログと観測で同じ差分表現を共有します。</summary>
        internal string FormatDiff(UiSnapshotDiff diff)
        {
            if (diff == null)
            {
                return "diff: -";
            }

            if (diff.isEmpty)
            {
                return "diff: empty";
            }

            var builder = _builder;
            builder.Clear();
            builder.Append("diff:");
            AppendStringArray(builder, " added", diff.addedPaths);
            AppendStringArray(builder, " removed", diff.removedPaths);
            if (diff.changed != null)
            {
                for (var changeIndex = 0; changeIndex < diff.changed.Length; changeIndex++)
                {
                    var change = diff.changed[changeIndex];
                    if (change == null)
                    {
                        continue;
                    }

                    builder.Append(" changed=");
                    builder.Append(change.path);
                    builder.Append(".");
                    builder.Append(change.field);
                }
            }

            if (!string.Equals(diff.focusedBefore, diff.focusedAfter, StringComparison.Ordinal))
            {
                builder.Append(" focus=");
                builder.Append(diff.focusedBefore);
                builder.Append("->");
                builder.Append(diff.focusedAfter);
            }

            if (!string.Equals(diff.sceneBefore, diff.sceneAfter, StringComparison.Ordinal))
            {
                builder.Append(" scene=");
                builder.Append(diff.sceneBefore);
                builder.Append("->");
                builder.Append(diff.sceneAfter);
            }

            return builder.ToString();
        }

        private static void AppendStringArray(StringBuilder builder, string label, string[] values)
        {
            if (values == null)
            {
                return;
            }

            for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                builder.Append(label);
                builder.Append("=");
                builder.Append(values[valueIndex]);
            }
        }

        /// <summary>操作の状態と観測を既存の応答形式へまとめます。</summary>
        internal string BuildStatusText(string status, string message, string observation)
        {
            var builder = _builder;
            builder.Clear();
            builder.Append("agent: status=");
            builder.Append(status);
            builder.Append(" message=");
            builder.AppendLine(message ?? string.Empty);
            builder.Append(observation ?? string.Empty);
            return builder.ToString().TrimEnd();
        }

        private static Dictionary<string, int> CountActionCandidateTargets(UiSnapshotElement[] elements)
        {
            var targetCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (elements == null)
            {
                return targetCounts;
            }

            for (var elementIndex = 0; elementIndex < elements.Length; elementIndex++)
            {
                var element = elements[elementIndex];
                if (!IsActionCandidate(element))
                {
                    continue;
                }

                var target = string.IsNullOrEmpty(element.path) ? element.name : element.path;
                if (string.IsNullOrEmpty(target))
                {
                    continue;
                }

                if (targetCounts.TryGetValue(target, out var count))
                {
                    targetCounts[target] = count + 1;
                    continue;
                }

                targetCounts.Add(target, 1);
            }

            return targetCounts;
        }

        private static bool IsActionCandidate(UiSnapshotElement element)
        {
            if (element == null)
            {
                return false;
            }

            if (element.clipped || element.offscreen || !element.interactable || !string.IsNullOrEmpty(element.blockedBy))
            {
                return false;
            }

            return element.kind == "Button" || element.kind == "Toggle" || element.kind == "Input" || element.kind == "Selectable";
        }

        /// <summary>観測と再生で共通の待機フレーム数を返します。</summary>
        internal static int ResolveSettleFrames(AgentOptions options)
        {
            return options.settleFrames > 0 ? options.settleFrames : DefaultSettleFrames;
        }
    }
}
#endif
