#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>観測文面と候補一覧を合成スナップショットで検証します。</summary>
    public sealed class AgentObservationFormatterTest
    {
        private const int SettleFrames = 4;

        /// <summary>同名候補はラベルで識別し、不可視要素や本文は操作候補から除外します。</summary>
        [Test]
        public void FullObservationIncludesLabelTargetsAndSettleFrames()
        {
            var snapshot = new UiSnapshotDocument
            {
                elements = new[]
                {
                    new UiSnapshotElement { path = "Start", kind = "Button", label = "開始", interactable = true },
                    new UiSnapshotElement { path = "Back", kind = "Button", label = "戻る", interactable = true },
                    new UiSnapshotElement { path = "Row", kind = "Selectable", label = "施設A", interactable = true },
                    new UiSnapshotElement { path = "Row", kind = "Selectable", label = "施設B", interactable = true },
                    new UiSnapshotElement { path = "Description", kind = "Text", label = "説明", interactable = true },
                    new UiSnapshotElement { path = "Masked", kind = "Button", label = "隠れた候補", interactable = true, clipped = true },
                },
            };
            var formatter = CreateFormatter();
            var observation = formatter.BuildFullObservation(snapshot);
            var expectedActions = string.Join(Environment.NewLine, new[]
            {
                "actions:",
                " - submit/click/tap target=Start label=開始",
                " - submit/click/tap target=Back label=戻る",
                " - submit/click/tap target=Row label=施設A → submit:\"label:施設A\"",
                " - submit/click/tap target=Row label=施設B → submit:\"label:施設B\"",
                " - press=south/east/north/west/start/select/leftShoulder/rightShoulder",
                " - move=up/down/left/right",
                " - stick=left/right x=-1..1 y=-1..1 seconds=0.1",
                "agent: settleFrames=4",
            });
            var actionStart = observation.IndexOf("actions:", StringComparison.Ordinal);
            Assert.That(actionStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(observation.Substring(actionStart), Is.EqualTo(expectedActions));
            Assert.That(formatter.BuildFullObservation(snapshot, "all"), Does.Contain("Masked"));
            Assert.That(formatter.BuildFullObservation(snapshot), Is.EqualTo(observation));
        }

        /// <summary>差分の入れ子整形や連続呼び出しでも共有バッファの内容を混ぜません。</summary>
        [Test]
        public void SharedBuilderPreservesDiffAndStatusText()
        {
            var formatter = CreateFormatter();
            var snapshot = new UiSnapshotDocument();
            var expected = string.Join(Environment.NewLine, new[]
            {
                "diff: empty",
                "game:",
                " -",
                "",
                "actions:",
                " - press=south/east/north/west/start/select/leftShoulder/rightShoulder",
                " - move=up/down/left/right",
                " - stick=left/right x=-1..1 y=-1..1 seconds=0.1",
                "agent: settleFrames=4",
            });
            Assert.That(formatter.BuildDiffObservation(snapshot, snapshot), Is.EqualTo(expected));
            var expectedStatus = "agent: status=running message=確認" + Environment.NewLine + expected;
            Assert.That(formatter.BuildStatusText("running", "確認", formatter.BuildDiffObservation(snapshot, snapshot)), Is.EqualTo(expectedStatus));
            Assert.That(formatter.BuildGoalFailureSummary(), Is.EqualTo("goalFailures: なし"));
            Assert.That(formatter.BuildDiffObservation(snapshot, snapshot), Is.EqualTo(expected));
        }

        private static AgentObservationFormatter CreateFormatter()
        {
            return new AgentObservationFormatter(new AgentGoal(), new AgentOptions { settleFrames = SettleFrames }, new AgentExpectationEvaluator(), () => false);
        }
    }
}
#endif
