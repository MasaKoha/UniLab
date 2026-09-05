#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>入力 API の差を吸収し、オーバーレイへ押下集合を渡します。</summary>
    internal sealed class InputOverlayLegacyInputSource
    {
        private readonly List<string> _pressedKeys = new List<string>();
        private readonly List<InputOverlayController.TouchSnapshot> _touches = new List<InputOverlayController.TouchSnapshot>();
        private readonly List<KeyCode> _keyboardKeyCodes = new List<KeyCode>();

        /// <summary>旧入力 API のキー一覧を一度だけ構築します。</summary>
        public InputOverlayLegacyInputSource()
        {
            var allKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));
            for (var keyIndex = 0; keyIndex < allKeyCodes.Length; keyIndex++)
            {
                var keyCode = allKeyCodes[keyIndex];
                if (keyCode >= KeyCode.Mouse0 && keyCode <= KeyCode.Mouse6)
                {
                    continue;
                }

                if (keyCode >= KeyCode.JoystickButton0 && keyCode <= KeyCode.Joystick8Button19)
                {
                    continue;
                }

                _keyboardKeyCodes.Add(keyCode);
            }
        }

        /// <summary>
        /// Input Manager だけでも最低限の可視化を維持します。
        /// Input System 非導入プロジェクトでも録画の診断価値を落とさないためです。
        /// </summary>
        public void Poll(InputOverlayController controller, float now)
        {
            _pressedKeys.Clear();
            for (var keyIndex = 0; keyIndex < _keyboardKeyCodes.Count; keyIndex++)
            {
                var keyCode = _keyboardKeyCodes[keyIndex];
                if (!Input.GetKey(keyCode))
                {
                    continue;
                }

                _pressedKeys.Add(NormalizeLegacyKeyName(keyCode));
            }

            controller.ReplacePressedKeyboardKeys(_pressedKeys, now);
            controller.SetPointerPosition(Input.mousePosition, now);
            controller.SetPointerButtons(Input.GetMouseButton(0), Input.GetMouseButton(1), Input.GetMouseButton(2), now);
            controller.ShowScroll(Input.mouseScrollDelta, now);

            _touches.Clear();
            for (var touchIndex = 0; touchIndex < Input.touchCount; touchIndex++)
            {
                var touch = Input.GetTouch(touchIndex);
                _touches.Add(new InputOverlayController.TouchSnapshot
                {
                    touchId = touch.fingerId,
                    position = touch.position,
                });
            }

            controller.ReplaceTouches(_touches, now);
            controller.UpdateGamepadButtonState("A", Input.GetKey(KeyCode.JoystickButton0), now);
            controller.UpdateGamepadButtonState("B", Input.GetKey(KeyCode.JoystickButton1), now);
            controller.UpdateGamepadButtonState("X", Input.GetKey(KeyCode.JoystickButton2), now);
            controller.UpdateGamepadButtonState("Y", Input.GetKey(KeyCode.JoystickButton3), now);
            controller.UpdateGamepadButtonState("LB", Input.GetKey(KeyCode.JoystickButton4), now);
            controller.UpdateGamepadButtonState("RB", Input.GetKey(KeyCode.JoystickButton5), now);
            controller.UpdateGamepadButtonState("Select", Input.GetKey(KeyCode.JoystickButton6), now);
            controller.UpdateGamepadButtonState("Start", Input.GetKey(KeyCode.JoystickButton7), now);
            controller.SetGamepadSticks(Vector2.zero, Vector2.zero, now);
        }

        private static string NormalizeLegacyKeyName(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Return:
                    return "Enter";
                case KeyCode.Escape:
                    return "Esc";
                case KeyCode.Space:
                    return "Space";
                default:
                    var keyName = keyCode.ToString();
                    return keyName.Length == 1 ? keyName.ToUpperInvariant() : keyName;
            }
        }
    }
}
#endif
