#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace UniLab.AI.Tests
{
    /// <summary>合成観測で事後条件の応答と同期一括実行を検証します。</summary>
    public sealed class AgentExpectTest
    {
        /// <summary>事後条件未達でも行動の成功を保持し、未達理由を JSON に含めます。</summary>
        [Test]
        public void FailedExpectationPreservesOkAndSerializesFailures()
        {
            var response = RunningResponse();
            var snapshot = new UiSnapshotDocument { activeScene = "Menu" };
            AgentActExpectation.Apply(response, new[] { new ScenarioExpectation { kind = "sceneIs", value = "Game" } }, snapshot, snapshot);
            var restored = JsonUtility.FromJson<AiCommandResponse>(JsonUtility.ToJson(response));
            Assert.That(restored.ok, Is.True);
            Assert.That(restored.expectOk, Is.False);
            Assert.That(restored.expectFailures, Has.Length.EqualTo(1));
            Assert.That(restored.expectFailures[0], Is.EqualTo(" - sceneIs target= value=Game message=シーンが一致しません。 actual=Menu"));
        }

        /// <summary>事後条件を省略した既存クライアントには成功と空配列を返します。</summary>
        [Test]
        public void OmittedExpectationsReturnTrueAndEmptyFailures()
        {
            var response = RunningResponse();
            AgentActExpectation.Apply(response, null, new UiSnapshotDocument(), new UiSnapshotDocument());
            var restored = JsonUtility.FromJson<AiCommandResponse>(JsonUtility.ToJson(response));
            Assert.That(restored.expectOk, Is.True);
            Assert.That(restored.expectFailures, Is.Empty);
        }

        /// <summary>二手目の未達で三手目を送らず、失敗した手の結果を返します。</summary>
        [Test]
        public void SecondStepExpectationFailureStopsImmediately()
        {
            var context = new AiCommandContext(new AiCommandRequest
            {
                op = "agent.act",
                args = "{\"steps\":[{\"submit\":\"First\",\"expect\":[{\"kind\":\"sceneIs\",\"value\":\"Menu\"}]},{\"submit\":\"Second\",\"expect\":[{\"kind\":\"sceneIs\",\"value\":\"Game\"}]},{\"submit\":\"Third\"}]}",
            });
            var executedCount = 0;
            var response = AiCommandDispatcher.ActImmediately(context, action =>
            {
                executedCount++;
                return RunningResponse();
            }, () => new UiSnapshotDocument { activeScene = "Menu" });
            Assert.That(executedCount, Is.EqualTo(2));
            Assert.That(response.ok, Is.True);
            Assert.That(response.expectOk, Is.False);
            Assert.That(response.message, Is.EqualTo("expect 未達で打ち切り"));
        }

        /// <summary>単一行動では引数直下と action 内の双方から事後条件を受け取れます。</summary>
        [TestCase("{\"action\":{\"submit\":\"Start\"},\"expect\":[{\"kind\":\"textVisible\",\"value\":\"完了\"}]}")]
        [TestCase("{\"action\":{\"submit\":\"Start\",\"expect\":[{\"kind\":\"textVisible\",\"value\":\"完了\"}]}}")]
        public void SingleActionAcceptsExpectations(string arguments)
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "agent.act", args = arguments });
            var response = AiCommandDispatcher.ActImmediately(context, action => RunningResponse(), () => new UiSnapshotDocument
            {
                elements = new[] { new UiSnapshotElement { path = "Canvas/Title", label = "完了" } },
            });
            Assert.That(response.expectOk, Is.True);
            Assert.That(context.GetActions()[0].expect, Has.Length.EqualTo(1));
        }

        /// <summary>changed は行動前後の差分で評価します。</summary>
        [Test]
        public void ChangedExpectationUsesBeforeAndAfterSnapshots()
        {
            var response = RunningResponse();
            var before = new UiSnapshotDocument { elements = new[] { new UiSnapshotElement { path = "Title", label = "前" } } };
            var after = new UiSnapshotDocument { elements = new[] { new UiSnapshotElement { path = "Title", label = "後" } } };
            AgentActExpectation.Apply(response, new[] { new ScenarioExpectation { kind = "changed" } }, before, after);
            Assert.That(response.expectOk, Is.True);
        }

        /// <summary>フォーカス条件も既存評価器で判定します。</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void FocusedExpectationUsesSnapshot(bool focused)
        {
            var snapshot = new UiSnapshotDocument
            {
                elements = new[] { new UiSnapshotElement { path = "Canvas/Start", name = "Start", focused = focused } },
            };
            var response = RunningResponse();
            AgentActExpectation.Apply(response, new[] { new ScenarioExpectation { kind = "focused", target = "Start" } }, snapshot, snapshot);
            Assert.That(response.expectOk, Is.EqualTo(focused));
            Assert.That(response.ok, Is.True);
        }

        private static AiCommandResponse RunningResponse()
        {
            return new AiCommandResponse { ok = true, op = "agent.act", text = "agent: status=running step=1" };
        }
    }
}
#endif
