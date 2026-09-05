#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace UniLab.AI
{
    /// <summary>行動の解釈と入力送出をセッション状態から分離します。</summary>
    internal sealed class AgentActionExecutor
    {
        private const float DefaultContinuousSeconds = 0.1f;

        private readonly Func<AgentSessionDriver> _ensureDriver;

        /// <summary>継続入力に必要なドライバを遅延取得できるようにします。</summary>
        internal AgentActionExecutor(Func<AgentSessionDriver> ensureDriver)
        {
            _ensureDriver = ensureDriver;
        }

        /// <summary>行動に対応する入力を送り、既存の応答文を返します。</summary>
        internal string ExecuteAction(AgentAction action)
        {
            if (action == null)
            {
                return "空の行動です。";
            }

            if (!string.IsNullOrEmpty(action.submit))
            {
                return ExecuteSubmit(action.submit);
            }

            if (!string.IsNullOrEmpty(action.scrollTo))
            {
                UiScrollTo.Execute(action.scrollTo, out var message);
                return message;
            }

            if (!InputInjector.IsSupported && HasRawInputAction(action))
            {
                return "Input System が有効ではないため入力を省略しました。";
            }

#if ENABLE_INPUT_SYSTEM
            if (!string.IsNullOrEmpty(action.press))
            {
                if (TryParseGamepadButton(action.press, out var gamepadButton))
                {
                    InputInjector.Press(gamepadButton);
                    return "press を送信しました。";
                }

                return "未対応の gamepad button です。";
            }

            if (!string.IsNullOrEmpty(action.hold))
            {
                if (TryParseGamepadButton(action.hold, out var gamepadButton))
                {
                    _ensureDriver().Run(InputInjector.Hold(gamepadButton, ResolveSeconds(action.seconds)));
                    return "hold を開始しました。";
                }

                return "未対応の hold button です。";
            }

            if (!string.IsNullOrEmpty(action.key))
            {
                if (TryParseKey(action.key, out var key))
                {
                    InputInjector.Key(key);
                    return "key を送信しました。";
                }

                return "未対応の key です。";
            }
#endif

            if (!string.IsNullOrEmpty(action.move))
            {
                InputInjector.Move(ParseDirection(action.move));
                return "move を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.stick))
            {
                _ensureDriver().Run(InputInjector.Stick(action.stick, action.x, action.y, ResolveSeconds(action.seconds)));
                return "stick を開始しました。";
            }

            if (!string.IsNullOrEmpty(action.text))
            {
                _ensureDriver().Run(InputInjector.Text(action.text));
                return "text を開始しました。";
            }

            if (!string.IsNullOrEmpty(action.pointerMove))
            {
                InputInjector.PointerMove(ResolveScreenPosition(action.pointerMove, action.x, action.y));
                return "pointerMove を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.click))
            {
                InputInjector.Click(ResolveScreenPosition(action.click, action.x, action.y), ParsePointerButton(action.button));
                return "click を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.scroll))
            {
                InputInjector.Scroll(ResolveScreenPosition(action.scroll, action.x, action.y), action.amount);
                return "scroll を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.tap))
            {
                InputInjector.Tap(ResolveScreenPosition(action.tap, action.x, action.y));
                return "tap を送信しました。";
            }

            if (!string.IsNullOrEmpty(action.drag))
            {
                var from = ResolveScreenPosition(action.from, action.fromX, action.fromY);
                var to = ResolveScreenPosition(action.to, action.toX, action.toY);
                _ensureDriver().Run(InputInjector.Drag(from, to, ResolveSeconds(action.seconds), ParsePointerButton(action.button)));
                return "drag を開始しました。";
            }

            if (!string.IsNullOrEmpty(action.swipe))
            {
                var from = ResolveScreenPosition(action.from, action.fromX, action.fromY);
                var to = ResolveScreenPosition(action.to, action.toX, action.toY);
                _ensureDriver().Run(InputInjector.Swipe(from, to, ResolveSeconds(action.seconds)));
                return "swipe を開始しました。";
            }

            if (!string.IsNullOrEmpty(action.pinch))
            {
                _ensureDriver().Run(InputInjector.Pinch(ResolveScreenPosition(action.center, action.x, action.y), action.fromDistance, action.toDistance, ResolveSeconds(action.seconds)));
                return "pinch を開始しました。";
            }

            return "解釈できる入力がありません。";
        }

        private string ExecuteSubmit(string targetName)
        {
            var target = UiInputLocator.FindTarget(targetName);
            if (target == null)
            {
                return $"submit 対象が見つかりません。 target={targetName}";
            }

            var blockingObject = UiInputLocator.FindBlockingObject(target);
            if (blockingObject != null)
            {
                return $"submit 対象が遮られています。 target={targetName} blockedBy={blockingObject.name}";
            }

            if (!UiInputLocator.IsInteractable(target))
            {
                return $"submit 対象が操作可能ではありません。 target={targetName}";
            }

            InputOverlay.SetStepLabel($"決定 [{targetName}]");
            return UiInputLocator.TrySubmit(target) ? "submit を送信しました。" : "submit を送れませんでした。";
        }

        private static bool HasRawInputAction(AgentAction action)
        {
            return action != null && (!string.IsNullOrEmpty(action.press)
                || !string.IsNullOrEmpty(action.hold)
                || !string.IsNullOrEmpty(action.move)
                || !string.IsNullOrEmpty(action.stick)
                || !string.IsNullOrEmpty(action.key)
                || !string.IsNullOrEmpty(action.text)
                || !string.IsNullOrEmpty(action.pointerMove)
                || !string.IsNullOrEmpty(action.click)
                || !string.IsNullOrEmpty(action.drag)
                || !string.IsNullOrEmpty(action.scroll)
                || !string.IsNullOrEmpty(action.tap)
                || !string.IsNullOrEmpty(action.swipe)
                || !string.IsNullOrEmpty(action.pinch));
        }

        /// <summary>複数指定時も既存の優先順位で行動種別を確定します。</summary>
        internal static string GetActionKind(AgentAction action)
        {
            if (action == null) { return string.Empty; }
            if (!string.IsNullOrEmpty(action.submit)) { return "submit"; }
            if (!string.IsNullOrEmpty(action.scrollTo)) { return "scrollTo"; }
            if (!string.IsNullOrEmpty(action.press)) { return "press"; }
            if (!string.IsNullOrEmpty(action.hold)) { return "hold"; }
            if (!string.IsNullOrEmpty(action.move)) { return "move"; }
            if (!string.IsNullOrEmpty(action.stick)) { return "stick"; }
            if (!string.IsNullOrEmpty(action.key)) { return "key"; }
            if (!string.IsNullOrEmpty(action.text)) { return "text"; }
            if (!string.IsNullOrEmpty(action.pointerMove)) { return "pointerMove"; }
            if (!string.IsNullOrEmpty(action.click)) { return "click"; }
            if (!string.IsNullOrEmpty(action.drag)) { return "drag"; }
            if (!string.IsNullOrEmpty(action.scroll)) { return "scroll"; }
            if (!string.IsNullOrEmpty(action.tap)) { return "tap"; }
            if (!string.IsNullOrEmpty(action.swipe)) { return "swipe"; }
            if (!string.IsNullOrEmpty(action.pinch)) { return "pinch"; }
            return string.Empty;
        }

        /// <summary>禁止判定とログで共通の対象表現を返します。</summary>
        internal static string GetActionTarget(AgentAction action)
        {
            if (action == null) { return string.Empty; }
            if (!string.IsNullOrEmpty(action.submit)) { return action.submit; }
            if (!string.IsNullOrEmpty(action.scrollTo)) { return action.scrollTo; }
            if (!string.IsNullOrEmpty(action.pointerMove)) { return action.pointerMove; }
            if (!string.IsNullOrEmpty(action.click)) { return action.click; }
            if (!string.IsNullOrEmpty(action.scroll)) { return action.scroll; }
            if (!string.IsNullOrEmpty(action.tap)) { return action.tap; }
            if (!string.IsNullOrEmpty(action.from)) { return action.from; }
            if (!string.IsNullOrEmpty(action.to)) { return action.to; }
            if (!string.IsNullOrEmpty(action.center)) { return action.center; }
            if (!string.IsNullOrEmpty(action.press)) { return action.press; }
            if (!string.IsNullOrEmpty(action.hold)) { return action.hold; }
            if (!string.IsNullOrEmpty(action.move)) { return action.move; }
            if (!string.IsNullOrEmpty(action.stick)) { return action.stick; }
            if (!string.IsNullOrEmpty(action.key)) { return action.key; }
            return string.Empty;
        }

        /// <summary>反復検出用に行動全体を同じ JSON 表現へ固定します。</summary>
        internal static string BuildActionKey(AgentAction action)
        {
            return action == null ? string.Empty : JsonUtility.ToJson(action, false);
        }

        private static Vector2 ResolveScreenPosition(string elementName, float fallbackX, float fallbackY)
        {
            if (!string.IsNullOrEmpty(elementName) && UiInputLocator.TryGetElementCenter(elementName, out var screenPosition))
            {
                return screenPosition;
            }

            return new Vector2(fallbackX, fallbackY);
        }

        private static FocusDirection ParseDirection(string direction)
        {
            switch (direction)
            {
                case "up": return FocusDirection.Up;
                case "down": return FocusDirection.Down;
                case "left": return FocusDirection.Left;
                case "right": return FocusDirection.Right;
                default: return FocusDirection.None;
            }
        }

        private static PointerButton ParsePointerButton(string button)
        {
            switch (button)
            {
                case "right": return PointerButton.Right;
                case "middle": return PointerButton.Middle;
                default: return PointerButton.Left;
            }
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>対応するゲームパッド語彙だけを入力として受け付けます。</summary>
        internal static bool TryParseGamepadButton(string value, out GamepadButton button)
        {
            switch (value)
            {
                case "south": button = GamepadButton.South; return true;
                case "east": button = GamepadButton.East; return true;
                case "north": button = GamepadButton.North; return true;
                case "west": button = GamepadButton.West; return true;
                case "dpadUp": button = GamepadButton.DpadUp; return true;
                case "dpadDown": button = GamepadButton.DpadDown; return true;
                case "dpadLeft": button = GamepadButton.DpadLeft; return true;
                case "dpadRight": button = GamepadButton.DpadRight; return true;
                case "leftShoulder": button = GamepadButton.LeftShoulder; return true;
                case "rightShoulder": button = GamepadButton.RightShoulder; return true;
                case "start": button = GamepadButton.Start; return true;
                case "select": button = GamepadButton.Select; return true;
                default: button = GamepadButton.South; return false;
            }
        }
#endif

#if ENABLE_INPUT_SYSTEM
        private static bool TryParseKey(string value, out Key key)
        {
            foreach (Key candidateKey in Enum.GetValues(typeof(Key)))
            {
                if (string.Equals(candidateKey.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    key = candidateKey;
                    return true;
                }
            }

            key = Key.None;
            return false;
        }
#endif

        private static float ResolveSeconds(float seconds)
        {
            return seconds > 0.0f ? seconds : DefaultContinuousSeconds;
        }
    }
}
#endif
