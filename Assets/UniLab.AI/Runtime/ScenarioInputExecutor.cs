#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace UniLab.AI
{
    /// <summary>
    /// UiScenarioStep の入力語彙を InputInjector 呼び出しへ変換して実行します。
    /// </summary>
    internal sealed class ScenarioInputExecutor
    {
        internal IEnumerator ExecuteInputCoroutine(UiScenarioStep step, Action<string, string, string, string, string> addFailure)
        {
            if (!string.IsNullOrEmpty(step.scrollTo))
            {
                if (!UiScrollTo.Execute(step.scrollTo, out var message))
                {
                    addFailure("scrollTo", step.scrollTo, string.Empty, message, string.Empty);
                }

                yield break;
            }

            if (!InputInjector.IsSupported && IsInputStep(step))
            {
                addFailure("input", GetInputKind(step), string.Empty, "Input System が有効ではありません。", string.Empty);
                yield break;
            }

#if ENABLE_INPUT_SYSTEM
            if (!string.IsNullOrEmpty(step.press))
            {
                if (TryParseGamepadButton(step.press, out var button))
                {
                    InputInjector.Press(button);
                }
                else
                {
                    addFailure("press", string.Empty, step.press, "未対応の gamepad button です。", string.Empty);
                }

                yield break;
            }

            if (!string.IsNullOrEmpty(step.hold))
            {
                if (TryParseGamepadButton(step.hold, out var button))
                {
                    yield return InputInjector.Hold(button, step.seconds);
                }
                else
                {
                    addFailure("hold", string.Empty, step.hold, "未対応の hold button です。", string.Empty);
                }

                yield break;
            }

            if (!string.IsNullOrEmpty(step.key))
            {
                if (TryParseKey(step.key, out var key))
                {
                    InputInjector.Key(key);
                }
                else
                {
                    addFailure("key", string.Empty, step.key, "未対応の key です。", string.Empty);
                }

                yield break;
            }
#endif

            if (!string.IsNullOrEmpty(step.move))
            {
                InputInjector.Move(ParseDirection(step.move));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.stick))
            {
                yield return InputInjector.Stick(step.stick, step.x, step.y, step.seconds);
                yield break;
            }

            if (!string.IsNullOrEmpty(step.text))
            {
                yield return InputInjector.Text(step.text);
                yield break;
            }

            if (!string.IsNullOrEmpty(step.pointerMove))
            {
                InputInjector.PointerMove(ResolveScreenPosition(step.pointerMove, step.x, step.y));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.click))
            {
                InputInjector.Click(ResolveScreenPosition(step.click, step.x, step.y), ParsePointerButton(step.button));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.scroll))
            {
                InputInjector.Scroll(ResolveScreenPosition(step.scroll, step.x, step.y), step.amount);
                yield break;
            }

            if (!string.IsNullOrEmpty(step.tap))
            {
                InputInjector.Tap(ResolveScreenPosition(step.tap, step.x, step.y));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.drag))
            {
                var from = ResolveScreenPosition(step.from, step.fromX, step.fromY);
                var to = ResolveScreenPosition(step.to, step.toX, step.toY);
                yield return InputInjector.Drag(from, to, step.seconds, ParsePointerButton(step.button));
                yield break;
            }

            if (!string.IsNullOrEmpty(step.swipe))
            {
                var from = ResolveScreenPosition(step.from, step.fromX, step.fromY);
                var to = ResolveScreenPosition(step.to, step.toX, step.toY);
                yield return InputInjector.Swipe(from, to, step.seconds);
                yield break;
            }

            if (!string.IsNullOrEmpty(step.pinch))
            {
                yield return InputInjector.Pinch(ResolveScreenPosition(step.center, step.x, step.y), step.fromDistance, step.toDistance, step.seconds);
                yield break;
            }

            if (UiScenarioStepReader.HasAnyAction(step))
            {
                addFailure("input", string.Empty, string.Empty, "解釈できる入力がありません。", string.Empty);
            }
        }

        internal static bool IsInputStep(UiScenarioStep step)
        {
            return !string.IsNullOrEmpty(step.scrollTo)
                || !string.IsNullOrEmpty(step.press)
                || !string.IsNullOrEmpty(step.hold)
                || !string.IsNullOrEmpty(step.move)
                || !string.IsNullOrEmpty(step.stick)
                || !string.IsNullOrEmpty(step.key)
                || !string.IsNullOrEmpty(step.text)
                || !string.IsNullOrEmpty(step.pointerMove)
                || !string.IsNullOrEmpty(step.click)
                || !string.IsNullOrEmpty(step.drag)
                || !string.IsNullOrEmpty(step.scroll)
                || !string.IsNullOrEmpty(step.tap)
                || !string.IsNullOrEmpty(step.swipe)
                || !string.IsNullOrEmpty(step.pinch);
        }

        internal static string GetInputKind(UiScenarioStep step)
        {
            if (!string.IsNullOrEmpty(step.scrollTo)) { return "scrollTo"; }
            if (!string.IsNullOrEmpty(step.press)) { return "press"; }
            if (!string.IsNullOrEmpty(step.hold)) { return "hold"; }
            if (!string.IsNullOrEmpty(step.move)) { return "move"; }
            if (!string.IsNullOrEmpty(step.stick)) { return "stick"; }
            if (!string.IsNullOrEmpty(step.key)) { return "key"; }
            if (!string.IsNullOrEmpty(step.text)) { return "text"; }
            if (!string.IsNullOrEmpty(step.pointerMove)) { return "pointerMove"; }
            if (!string.IsNullOrEmpty(step.click)) { return "click"; }
            if (!string.IsNullOrEmpty(step.drag)) { return "drag"; }
            if (!string.IsNullOrEmpty(step.scroll)) { return "scroll"; }
            if (!string.IsNullOrEmpty(step.tap)) { return "tap"; }
            if (!string.IsNullOrEmpty(step.swipe)) { return "swipe"; }
            if (!string.IsNullOrEmpty(step.pinch)) { return "pinch"; }
            return string.Empty;
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
        private static bool TryParseGamepadButton(string buttonName, out GamepadButton button)
        {
            switch (buttonName)
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

        private static bool TryParseKey(string keyName, out Key key)
        {
            foreach (Key candidateKey in Enum.GetValues(typeof(Key)))
            {
                if (string.Equals(candidateKey.ToString(), keyName, StringComparison.OrdinalIgnoreCase))
                {
                    key = candidateKey;
                    return true;
                }
            }

            key = Key.None;
            return false;
        }
#endif
    }
}
#endif
