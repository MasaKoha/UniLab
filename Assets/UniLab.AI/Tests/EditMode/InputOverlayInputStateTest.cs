#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>描画や入力デバイスなしで押下集合の保持を検証します。</summary>
    public sealed class InputOverlayInputStateTest
    {
        private const float PressedAt = 1f;
        private const float PartiallyReleasedAt = 2f;
        private const float ReleasedAt = 3f;
        private const float AfterHoldAt = 10f;

        /// <summary>同時押しの片方を離しても他方を保持し、全解放後に表示も消えます。</summary>
        [Test]
        public void SimultaneousKeysRemainPressedUntilEachIsReleased()
        {
            var history = new List<string>();
            var state = new InputOverlayInputState((label, now) => history.Add(label), now => { });
            var options = new InputOverlayOptions();
            state.ApplyOptions(options);
            state.ReplacePressedKeyboardKeys(new List<string> { "A", "B" }, PressedAt);
            Assert.That(state.KeyboardStatesByKey["A"].isPressed, Is.True);
            Assert.That(state.KeyboardStatesByKey["B"].isPressed, Is.True);
            state.ReplacePressedKeyboardKeys(new List<string> { "B" }, PartiallyReleasedAt);
            Assert.That(state.KeyboardStatesByKey["A"].isPressed, Is.False);
            Assert.That(state.KeyboardStatesByKey["B"].isPressed, Is.True);
            Assert.That(state.KeyboardStatesByKey["A"].IsVisible(PartiallyReleasedAt, options.holdSeconds), Is.True);
            state.ReplacePressedKeyboardKeys(new List<string>(), ReleasedAt);
            Assert.That(state.KeyboardStatesByKey["B"].isPressed, Is.False);
            Assert.That(state.KeyboardStatesByKey["A"].IsVisible(AfterHoldAt, options.holdSeconds), Is.False);
            Assert.That(state.KeyboardStatesByKey["B"].IsVisible(AfterHoldAt, options.holdSeconds), Is.False);
            Assert.That(history, Is.EqualTo(new[] { "A", "B" }));
        }
    }
}
#endif
