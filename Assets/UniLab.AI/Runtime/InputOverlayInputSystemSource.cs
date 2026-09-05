#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>入力 API の差を吸収し、オーバーレイへ押下集合を渡します。</summary>
    internal sealed class InputOverlayInputSystemSource
    {
        private readonly List<string> _pressedKeys = new List<string>();
        private readonly List<InputOverlayController.TouchSnapshot> _touches = new List<InputOverlayController.TouchSnapshot>();
        private Type _keyboardType;
        private Type _mouseType;
        private Type _gamepadType;
        private Type _touchscreenType;
        private bool _isInitialized;
        private bool _isAvailable;

        /// <summary>
        /// Input System がある場合はそちらを優先します。
        /// 注入入力と実機入力を同じ状態経路で観測するためです。
        /// </summary>
        public bool TryPoll(InputOverlayController controller, float now)
        {
            EnsureInitialized();
            if (!_isAvailable)
            {
                return false;
            }

            PollKeyboard(controller, now);
            PollMouse(controller, now);
            PollGamepad(controller, now);
            PollTouches(controller, now);
            return true;
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            _keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            _mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            _gamepadType = Type.GetType("UnityEngine.InputSystem.Gamepad, Unity.InputSystem");
            _touchscreenType = Type.GetType("UnityEngine.InputSystem.Touchscreen, Unity.InputSystem");
            _isAvailable = _keyboardType != null || _mouseType != null || _gamepadType != null || _touchscreenType != null;
        }

        private void PollKeyboard(InputOverlayController controller, float now)
        {
            _pressedKeys.Clear();
            var keyboard = GetCurrentDevice(_keyboardType);
            if (keyboard == null)
            {
                controller.ReplacePressedKeyboardKeys(_pressedKeys, now);
                return;
            }

            var allKeys = GetMemberValue(keyboard, "allKeys") as IEnumerable;
            if (allKeys != null)
            {
                foreach (var keyControl in allKeys)
                {
                    if (!ReadBooleanMember(keyControl, "isPressed"))
                    {
                        continue;
                    }

                    var displayName = GetStringMember(keyControl, "displayName");
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = GetStringMember(keyControl, "name");
                    }

                    if (string.IsNullOrEmpty(displayName))
                    {
                        continue;
                    }

                    _pressedKeys.Add(NormalizeInputSystemKeyName(displayName));
                }
            }

            controller.ReplacePressedKeyboardKeys(_pressedKeys, now);
        }

        private void PollMouse(InputOverlayController controller, float now)
        {
            var mouse = GetCurrentDevice(_mouseType);
            if (mouse == null)
            {
                return;
            }

            controller.SetPointerPosition(ReadVector2Control(mouse, "position"), now);
            controller.SetPointerButtons(
                ReadButtonControl(mouse, "leftButton"),
                ReadButtonControl(mouse, "rightButton"),
                ReadButtonControl(mouse, "middleButton"),
                now);
            controller.ShowScroll(ReadVector2Control(mouse, "scroll"), now);
        }

        private void PollGamepad(InputOverlayController controller, float now)
        {
            var gamepad = GetCurrentDevice(_gamepadType);
            if (gamepad == null)
            {
                controller.UpdateGamepadButtonState("A", false, now);
                controller.UpdateGamepadButtonState("B", false, now);
                controller.UpdateGamepadButtonState("X", false, now);
                controller.UpdateGamepadButtonState("Y", false, now);
                controller.UpdateGamepadButtonState("LB", false, now);
                controller.UpdateGamepadButtonState("RB", false, now);
                controller.UpdateGamepadButtonState("Start", false, now);
                controller.UpdateGamepadButtonState("Select", false, now);
                controller.UpdateGamepadButtonState("Up", false, now);
                controller.UpdateGamepadButtonState("Down", false, now);
                controller.UpdateGamepadButtonState("Left", false, now);
                controller.UpdateGamepadButtonState("Right", false, now);
                controller.SetGamepadSticks(Vector2.zero, Vector2.zero, now);
                return;
            }

            UpdateButton(controller, gamepad, "buttonSouth", "A", now);
            UpdateButton(controller, gamepad, "buttonEast", "B", now);
            UpdateButton(controller, gamepad, "buttonWest", "X", now);
            UpdateButton(controller, gamepad, "buttonNorth", "Y", now);
            UpdateButton(controller, gamepad, "leftShoulder", "LB", now);
            UpdateButton(controller, gamepad, "rightShoulder", "RB", now);
            UpdateButton(controller, gamepad, "startButton", "Start", now);
            UpdateButton(controller, gamepad, "selectButton", "Select", now);
            var dpad = GetMemberValue(gamepad, "dpad");
            UpdateButton(controller, dpad, "up", "Up", now);
            UpdateButton(controller, dpad, "down", "Down", now);
            UpdateButton(controller, dpad, "left", "Left", now);
            UpdateButton(controller, dpad, "right", "Right", now);

            controller.SetGamepadSticks(
                ReadVector2Control(gamepad, "leftStick"),
                ReadVector2Control(gamepad, "rightStick"),
                now);
        }

        private void PollTouches(InputOverlayController controller, float now)
        {
            _touches.Clear();
            var touchscreen = GetCurrentDevice(_touchscreenType);
            if (touchscreen == null)
            {
                controller.ReplaceTouches(_touches, now);
                return;
            }

            var touches = GetMemberValue(touchscreen, "touches");
            if (touches == null)
            {
                controller.ReplaceTouches(_touches, now);
                return;
            }

            var countProperty = touches.GetType().GetProperty("Count");
            var itemProperty = touches.GetType().GetProperty("Item");
            if (countProperty == null || itemProperty == null)
            {
                controller.ReplaceTouches(_touches, now);
                return;
            }

            var touchCount = (int)countProperty.GetValue(touches, null);
            for (var touchIndex = 0; touchIndex < touchCount; touchIndex++)
            {
                var touchControl = itemProperty.GetValue(touches, new object[] { touchIndex });
                if (!ReadButtonControl(touchControl, "press"))
                {
                    continue;
                }

                var touchId = Mathf.RoundToInt(ReadFloatControl(touchControl, "touchId"));
                _touches.Add(new InputOverlayController.TouchSnapshot
                {
                    touchId = touchId,
                    position = ReadVector2Control(touchControl, "position"),
                });
            }

            controller.ReplaceTouches(_touches, now);
        }

        private static object GetCurrentDevice(Type deviceType)
        {
            if (deviceType == null)
            {
                return null;
            }

            var currentProperty = deviceType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            return currentProperty == null ? null : currentProperty.GetValue(null, null);
        }

        private static void UpdateButton(InputOverlayController controller, object deviceOrControl, string memberName, string overlayKey, float now)
        {
            controller.UpdateGamepadButtonState(overlayKey, ReadButtonControl(deviceOrControl, memberName), now);
        }

        private static bool ReadButtonControl(object deviceOrControl, string memberName)
        {
            var control = GetMemberValue(deviceOrControl, memberName);
            return ReadBooleanMember(control, "isPressed");
        }

        private static float ReadFloatControl(object deviceOrControl, string memberName)
        {
            var control = GetMemberValue(deviceOrControl, memberName);
            var value = ReadControlValue(control);
            if (value is float floatValue)
            {
                return floatValue;
            }

            if (value is double doubleValue)
            {
                return (float)doubleValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            return 0f;
        }

        private static Vector2 ReadVector2Control(object deviceOrControl, string memberName)
        {
            var control = GetMemberValue(deviceOrControl, memberName);
            var value = ReadControlValue(control);
            return value is Vector2 vector2Value ? vector2Value : Vector2.zero;
        }

        private static object ReadControlValue(object control)
        {
            if (control == null)
            {
                return null;
            }

            var method = control.GetType().GetMethod("ReadValueAsObject", BindingFlags.Public | BindingFlags.Instance);
            return method == null ? null : method.Invoke(control, null);
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
            {
                return null;
            }

            var property = instance.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            return property == null ? null : property.GetValue(instance, null);
        }

        private static bool ReadBooleanMember(object instance, string memberName)
        {
            if (instance == null)
            {
                return false;
            }

            var property = instance.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return false;
            }

            var value = property.GetValue(instance, null);
            return value is bool boolValue && boolValue;
        }

        private static string GetStringMember(object instance, string memberName)
        {
            if (instance == null)
            {
                return string.Empty;
            }

            var property = instance.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return string.Empty;
            }

            return property.GetValue(instance, null) as string ?? string.Empty;
        }

        private static string NormalizeInputSystemKeyName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return string.Empty;
            }

            switch (rawName)
            {
                case "Escape":
                    return "Esc";
                case "Enter":
                case "Return":
                    return "Enter";
                default:
                    return rawName.Length == 1 ? rawName.ToUpperInvariant() : rawName;
            }
        }
    }
}
#endif
