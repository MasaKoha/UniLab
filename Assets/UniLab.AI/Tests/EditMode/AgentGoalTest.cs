#if UNITY_EDITOR
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>PR4 の観測契約をシーンなしで検証します。</summary>
    public sealed class AgentGoalTest
    {
        /// <summary>自由行動だけが空の期待値を許可します。</summary>
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void EmptyExpectationsRequireFreePlay(bool freePlay, bool expectedValid)
        {
            var goal = new AgentGoal { freePlay = freePlay };
            Assert.That(AgentGoalValidator.Validate(goal, out var message), Is.EqualTo(expectedValid));
            Assert.That(string.IsNullOrEmpty(message), Is.EqualTo(expectedValid));
            goal.goal = System.Array.Empty<ScenarioExpectation>();
            Assert.That(AgentGoalValidator.Validate(goal, out _), Is.EqualTo(expectedValid));
        }

        /// <summary>自由行動の Begin は空目標拒否を通過し、Play 判定に到達します。</summary>
        [Test]
        public void FreePlayBeginReachesPlayModeValidation()
        {
            var result = AgentSessionCommands.Begin("{\"freePlay\":true}", string.Empty);
            Assert.That(result, Does.Contain("playMode が必要です"));
            Assert.That(result, Does.Not.Contain("期待値がありません"));
        }

        /// <summary>従来の空目標は Play 判定より先に拒否されます。</summary>
        [Test]
        public void EmptyGoalBeginRejectsBeforePlayModeValidation()
        {
            var result = AgentSessionCommands.Begin("{}", string.Empty);
            Assert.That(result, Does.Contain("期待値がありません"));
            Assert.That(result, Does.Not.Contain("playMode が必要です"));
        }

        /// <summary>通常目標の期待値を引き続き受理し、null は拒否します。</summary>
        [Test]
        public void RegularGoalRequiresNonNullExpectations()
        {
            Assert.That(AgentGoalValidator.Validate(null, out _), Is.False);
            var goal = new AgentGoal { goal = new[] { new ScenarioExpectation { kind = "textVisible", value = "完了" } } };
            Assert.That(AgentGoalValidator.Validate(goal, out _), Is.True);
        }

        /// <summary>自由行動では指定された未達条件も観測ノイズにしません。</summary>
        [Test]
        public void FreePlayOmitsGoalFailuresFromFullAndDiffObservation()
        {
            var goal = new AgentGoal
            {
                freePlay = true,
                goal = new[] { new ScenarioExpectation { kind = "textVisible", value = "__never__" } },
            };
            var formatter = new AgentObservationFormatter(goal, new AgentOptions(), new AgentExpectationEvaluator(), () => false);
            var snapshot = new UiSnapshotDocument();
            Assert.That(formatter.BuildFullObservation(snapshot), Does.Not.Contain("goalFailures:"));
            Assert.That(formatter.BuildDiffObservation(snapshot, snapshot), Does.Not.Contain("goalFailures:"));
        }
    }
}
#endif
