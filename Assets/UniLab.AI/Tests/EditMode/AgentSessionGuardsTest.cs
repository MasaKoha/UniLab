#if UNITY_EDITOR
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>ゲーム実行なしで予算と禁止語による停止条件を検証します。</summary>
    public sealed class AgentSessionGuardsTest
    {
        /// <summary>反復上限には初回も数える既存の停止タイミングを固定します。</summary>
        [Test]
        public void StuckRepeatLimitIncludesFirstObservation()
        {
            const int stuckRepeatLimit = 2;
            var guards = new AgentSessionGuards(new AgentGoal(), new AgentOptions { stuckRepeatLimit = stuckRepeatLimit });
            Assert.That(guards.IsStuck("画面A", "決定"), Is.False);
            Assert.That(guards.IsStuck("画面A", "決定"), Is.True);
            Assert.That(guards.IsStuck("画面A", "決定"), Is.True);
        }

        /// <summary>行動を変えても同じ観測の反復を止め、観測が変われば履歴をリセットします。</summary>
        [Test]
        public void RepeatedObservationStopsDifferentActionsAndResetsOnChange()
        {
            const int stuckRepeatLimit = 2;
            var guards = new AgentSessionGuards(new AgentGoal(), new AgentOptions { stuckRepeatLimit = stuckRepeatLimit });
            Assert.That(guards.IsStuck("画面A", "決定"), Is.False);
            Assert.That(guards.IsStuck("画面A", "戻る"), Is.True);
            Assert.That(guards.IsStuck("画面B", "戻る"), Is.False);
        }

        /// <summary>禁止語は行動キーと対象のどちらでも部分一致させます。</summary>
        [TestCase("submit:DELETE_SAVE", "Save", true)]
        [TestCase("submit", "ConfirmDeleteButton", true)]
        [TestCase("submit", "Continue", false)]
        [TestCase(null, null, false)]
        public void ForbiddenWordsMatchIgnoringCase(string actionKey, string target, bool expected)
        {
            var goal = new AgentGoal { forbid = new[] { null, string.Empty, "delete" } };
            var guards = new AgentSessionGuards(goal, new AgentOptions());
            Assert.That(guards.IsForbidden(actionKey, target), Is.EqualTo(expected));
        }

        /// <summary>未指定と無効な予算には既存の既定値を適用します。</summary>
        [TestCase(0, 0, 200, 600)]
        [TestCase(-1, -1, 200, 600)]
        [TestCase(5, 10, 5, 10)]
        public void BudgetsResolveDefaults(int maxSteps, int maxSeconds, int expectedSteps, int expectedSeconds)
        {
            var goal = new AgentGoal { maxSteps = maxSteps, maxSeconds = maxSeconds };
            var guards = new AgentSessionGuards(goal, new AgentOptions());
            Assert.That(guards.ResolveMaxSteps(), Is.EqualTo(expectedSteps));
            Assert.That(guards.ResolveMaxSeconds(), Is.EqualTo(expectedSeconds));
            Assert.That(guards.IsStepBudgetExceeded(expectedSteps - 1), Is.False);
            Assert.That(guards.IsStepBudgetExceeded(expectedSteps), Is.True);
            const double startedAtRealtime = 100.0;
            const double secondsBeforeLimit = 0.5;
            Assert.That(guards.IsTimeBudgetExceeded(startedAtRealtime, startedAtRealtime + expectedSeconds - secondsBeforeLimit), Is.False);
            Assert.That(guards.IsTimeBudgetExceeded(startedAtRealtime, startedAtRealtime + expectedSeconds), Is.True);
        }
    }
}
#endif
