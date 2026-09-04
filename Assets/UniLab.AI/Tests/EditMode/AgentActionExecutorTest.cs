#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.LowLevel;
#endif

namespace UniLab.AI.Tests
{
    /// <summary>入力送出なしで行動の解釈と反復キーの契約を検証します。</summary>
    public sealed class AgentActionExecutorTest
    {
        /// <summary>種別・対象・キーが既存の行動表現に一致することを検証します。</summary>
        [TestCase("{\"submit\":\"Start\"}", "submit", "Start")]
        [TestCase("{\"press\":\"south\"}", "press", "south")]
        [TestCase("{\"move\":\"up\"}", "move", "up")]
        [TestCase("{\"click\":\"Row\"}", "click", "Row")]
        [TestCase("{\"drag\":\"pointer\",\"from\":\"First\",\"to\":\"Last\"}", "drag", "First")]
        [TestCase("{}", "", "")]
        [TestCase(null, "", "")]
        [TestCase("{\"submit\":\"Start\",\"press\":\"south\"}", "submit", "Start")]
        public void ActionIdentityMatchesContract(string actionJson, string expectedKind, string expectedTarget)
        {
            var action = actionJson == null ? null : JsonUtility.FromJson<AgentAction>(actionJson);
            Assert.That(AgentActionExecutor.GetActionKind(action), Is.EqualTo(expectedKind));
            Assert.That(AgentActionExecutor.GetActionTarget(action), Is.EqualTo(expectedTarget));
            var expectedKey = action == null ? string.Empty : JsonUtility.ToJson(action, false);
            Assert.That(AgentActionExecutor.BuildActionKey(action), Is.EqualTo(expectedKey));
        }

        /// <summary>対象が同じでも入力の引数や理由の違いを反復キーに残します。</summary>
        [Test]
        public void ActionKeyPreservesArgumentsAndReason()
        {
            var action = new AgentAction { click = "Row", button = "left", reason = "選択" };
            var originalKey = AgentActionExecutor.BuildActionKey(action);
            action.button = "right";
            var changedButtonKey = AgentActionExecutor.BuildActionKey(action);
            action.reason = "確認";
            Assert.That(changedButtonKey, Is.Not.EqualTo(originalKey));
            Assert.That(AgentActionExecutor.BuildActionKey(action), Is.Not.EqualTo(changedButtonKey));
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>既知のボタンと未知語を区別して入力の誤解釈を防ぎます。</summary>
        [TestCase("south", true, GamepadButton.South)]
        [TestCase("unknown", false, GamepadButton.South)]
        public void GamepadButtonMatchesVocabulary(string value, bool expectedSuccess, GamepadButton expectedButton)
        {
            var success = AgentActionExecutor.TryParseGamepadButton(value, out var button);
            Assert.That(success, Is.EqualTo(expectedSuccess));
            Assert.That(button, Is.EqualTo(expectedButton));
        }
#endif
    }
}
#endif
