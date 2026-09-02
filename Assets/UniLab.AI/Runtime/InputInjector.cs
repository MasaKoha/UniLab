using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace UniLab.AI
{
    /// <summary>
    /// 実機と同じ Input System 経路へ流し込むことで、UI だけでなくゲーム側の入力解決そのものを検証するための注入器です。
    /// </summary>
    public static class InputInjector
    {
#if ENABLE_INPUT_SYSTEM
        private const float DefaultClickFrameDelaySeconds = 0.0f;

        private static readonly HashSet<Key> PressedKeys = new HashSet<Key>();

        private static Gamepad _gamepad;
        private static Keyboard _keyboard;
        private static Mouse _mouse;
        private static Touchscreen _touchscreen;
        private static InputInjectorDriver _driver;
        private static GamepadState _gamepadState;
        private static MouseState _mouseState;
#endif

        /// <summary>
        /// Input System が無いプロジェクトでも呼び出し側を分岐できるようにするための対応可否です。
        /// </summary>
        public static bool IsSupported
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// 単打を次フレーム解放にし、wasPressedThisFrame / wasReleasedThisFrame の両方を自然に通すためのボタン入力です。
        /// </summary>
        public static void Press(GamepadButton button)
        {
#if ENABLE_INPUT_SYSTEM
            EnsureDriver();
            _driver.StartCoroutine(PressCoroutine(button));
#endif
        }

        /// <summary>
        /// 長押しは UI のホールド分岐や戻る長押しを検証するため、継続時間を明示して送ります。
        /// </summary>
        public static IEnumerator Hold(GamepadButton button, float seconds)
        {
#if ENABLE_INPUT_SYSTEM
            var gamepad = EnsureGamepad();
            SetGamepadButton(button, true);
            InputSystem.QueueStateEvent(gamepad, _gamepadState);
            InputSystem.Update();
            yield return WaitForSeconds(seconds);
            SetGamepadButton(button, false);
            InputSystem.QueueStateEvent(gamepad, _gamepadState);
            InputSystem.Update();
#else
            yield break;
#endif
        }

        /// <summary>
        /// move を D-Pad 単打へ正規化し、フォーカス移動をボタン語彙と同じ経路に乗せるための入力です。
        /// </summary>
        public static void Move(FocusDirection direction)
        {
#if ENABLE_INPUT_SYSTEM
            Press(ResolveDirectionButton(direction));
#endif
        }

        /// <summary>
        /// キー単打を次フレーム解放にし、Submit や Escape のような 1 発入力を実機同様に扱うための入力です。
        /// </summary>
        public static void Key(Key key)
        {
#if ENABLE_INPUT_SYSTEM
            EnsureDriver();
            _driver.StartCoroutine(KeyCoroutine(key));
#endif
        }

        /// <summary>
        /// スティック入力を一定時間維持し、アナログ移動や慣性付き UI を実機同様に通すための入力です。
        /// </summary>
        public static IEnumerator Stick(string axisName, float x, float y, float seconds)
        {
#if ENABLE_INPUT_SYSTEM
            var gamepad = EnsureGamepad();
            var startRealtime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startRealtime < seconds)
            {
                if (axisName == "right")
                {
                    _gamepadState.rightStick = new Vector2(x, y);
                }
                else
                {
                    _gamepadState.leftStick = new Vector2(x, y);
                }

                InputSystem.QueueStateEvent(gamepad, _gamepadState);
                InputSystem.Update();
                yield return null;
            }

            if (axisName == "right")
            {
                _gamepadState.rightStick = Vector2.zero;
            }
            else
            {
                _gamepadState.leftStick = Vector2.zero;
            }

            InputSystem.QueueStateEvent(gamepad, _gamepadState);
            InputSystem.Update();
#else
            yield break;
#endif
        }

        /// <summary>
        /// 文字列は TextEvent を 1 文字ずつ送ることで、TMP_InputField へ OS 非依存に文字を入れるための入力です。
        /// </summary>
        public static IEnumerator Text(string text)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = EnsureKeyboard();
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            for (var characterIndex = 0; characterIndex < text.Length; characterIndex++)
            {
                InputSystem.QueueTextEvent(keyboard, text[characterIndex]);
                InputSystem.Update();
                yield return null;
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// ポインタ移動を先に行うことで、hover 解決や currentMouse 依存の UI を自然な順で通すための入力です。
        /// </summary>
        public static void PointerMove(Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = EnsureMouse();
            var delta = screenPosition - _mouseState.position;
            _mouseState.position = screenPosition;
            _mouseState.delta = delta;
            InputSystem.QueueStateEvent(mouse, _mouseState);
            InputSystem.Update();
            _mouseState.delta = Vector2.zero;
#endif
        }

        /// <summary>
        /// クリックは移動と押下解放を 1 つの操作にまとめ、要素名指定時の JSON を簡潔に保つための入力です。
        /// </summary>
        public static void Click(Vector2 screenPosition, PointerButton button = PointerButton.Left)
        {
#if ENABLE_INPUT_SYSTEM
            EnsureDriver();
            _driver.StartCoroutine(ClickCoroutine(screenPosition, button));
#endif
        }

        /// <summary>
        /// ドラッグは押下中の軌跡が本体であり、途中フレームも送ってスクロールやスライダを実機同様に動かすための入力です。
        /// </summary>
        public static IEnumerator Drag(Vector2 from, Vector2 to, float seconds, PointerButton button = PointerButton.Left)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = EnsureMouse();
            PointerMove(from);
            SetMouseButton(button, true);
            InputSystem.QueueStateEvent(mouse, _mouseState);
            InputSystem.Update();

            if (seconds <= 0.0f)
            {
                PointerMove(to);
            }
            else
            {
                var startRealtime = Time.realtimeSinceStartup;
                while (true)
                {
                    var elapsedSeconds = Time.realtimeSinceStartup - startRealtime;
                    var normalized = Mathf.Clamp01(elapsedSeconds / seconds);
                    PointerMove(Vector2.Lerp(from, to, normalized));
                    if (normalized >= 1.0f)
                    {
                        break;
                    }

                    yield return null;
                }
            }

            SetMouseButton(button, false);
            InputSystem.QueueStateEvent(mouse, _mouseState);
            InputSystem.Update();
#else
            yield break;
#endif
        }

        /// <summary>
        /// スクロールは位置と同時に送ることで、ポインタ位置依存 UI でも対象を外さないための入力です。
        /// </summary>
        public static void Scroll(Vector2 screenPosition, float amount)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = EnsureMouse();
            PointerMove(screenPosition);
            _mouseState.scroll = new Vector2(0.0f, amount);
            InputSystem.QueueStateEvent(mouse, _mouseState);
            InputSystem.Update();
            _mouseState.scroll = Vector2.zero;
#endif
        }

        /// <summary>
        /// 1 指タップを Touchscreen へ送ることで、マウス前提コードと区別されるタッチ UI も検証できるようにします。
        /// </summary>
        public static void Tap(Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            EnsureDriver();
            _driver.StartCoroutine(TapCoroutine(screenPosition));
#endif
        }

        /// <summary>
        /// スワイプは 1 指の軌跡を複数フレームで送り、ページ送りやスクロール判定が velocity を見ても再現できるようにします。
        /// </summary>
        public static IEnumerator Swipe(Vector2 from, Vector2 to, float seconds)
        {
#if ENABLE_INPUT_SYSTEM
            var touchscreen = EnsureTouchscreen();
            var startTime = Time.realtimeSinceStartupAsDouble;
            var previousPosition = from;
            QueueTouchState(touchscreen, 1, TouchPhase.Began, from, Vector2.zero, startTime, from);
            yield return null;

            if (seconds > 0.0f)
            {
                while (true)
                {
                    var elapsedSeconds = Time.realtimeSinceStartupAsDouble - startTime;
                    var normalized = Mathf.Clamp01((float)(elapsedSeconds / seconds));
                    var currentPosition = Vector2.Lerp(from, to, normalized);
                    QueueTouchState(touchscreen, 1, TouchPhase.Moved, currentPosition, currentPosition - previousPosition, startTime, from);
                    previousPosition = currentPosition;
                    if (normalized >= 1.0f)
                    {
                        break;
                    }

                    yield return null;
                }
            }

            QueueTouchState(touchscreen, 1, TouchPhase.Ended, to, to - previousPosition, startTime, from);
#else
            yield break;
#endif
        }

        /// <summary>
        /// ピンチは 2 指を対称に動かし、ズーム系 UI をタッチ専用の経路で検証できるようにします。
        /// </summary>
        public static IEnumerator Pinch(Vector2 center, float fromDistance, float toDistance, float seconds)
        {
#if ENABLE_INPUT_SYSTEM
            var touchscreen = EnsureTouchscreen();
            var startTime = Time.realtimeSinceStartupAsDouble;
            var currentFromDistance = fromDistance;
            var startFirst = center + Vector2.left * (fromDistance * 0.5f);
            var startSecond = center + Vector2.right * (fromDistance * 0.5f);
            QueueTouchState(touchscreen, 1, TouchPhase.Began, startFirst, Vector2.zero, startTime, startFirst);
            QueueTouchState(touchscreen, 2, TouchPhase.Began, startSecond, Vector2.zero, startTime, startSecond);
            yield return null;

            if (seconds > 0.0f)
            {
                while (true)
                {
                    var elapsedSeconds = Time.realtimeSinceStartupAsDouble - startTime;
                    var normalized = Mathf.Clamp01((float)(elapsedSeconds / seconds));
                    currentFromDistance = Mathf.Lerp(fromDistance, toDistance, normalized);
                    var first = center + Vector2.left * (currentFromDistance * 0.5f);
                    var second = center + Vector2.right * (currentFromDistance * 0.5f);
                    QueueTouchState(touchscreen, 1, TouchPhase.Moved, first, Vector2.zero, startTime, startFirst);
                    QueueTouchState(touchscreen, 2, TouchPhase.Moved, second, Vector2.zero, startTime, startSecond);
                    if (normalized >= 1.0f)
                    {
                        break;
                    }

                    yield return null;
                }
            }

            var endFirst = center + Vector2.left * (toDistance * 0.5f);
            var endSecond = center + Vector2.right * (toDistance * 0.5f);
            QueueTouchState(touchscreen, 1, TouchPhase.Ended, endFirst, Vector2.zero, startTime, startFirst);
            QueueTouchState(touchscreen, 2, TouchPhase.Ended, endSecond, Vector2.zero, startTime, startSecond);
#else
            yield break;
#endif
        }

        /// <summary>
        /// 仮想デバイスを必ず外し、次回 Play に幽霊 current デバイスを残さないための終了処理です。
        /// </summary>
        public static void Dispose()
        {
#if ENABLE_INPUT_SYSTEM
            if (_gamepad != null)
            {
                InputSystem.RemoveDevice(_gamepad);
                _gamepad = null;
            }

            if (_keyboard != null)
            {
                InputSystem.RemoveDevice(_keyboard);
                _keyboard = null;
            }

            if (_mouse != null)
            {
                InputSystem.RemoveDevice(_mouse);
                _mouse = null;
            }

            if (_touchscreen != null)
            {
                InputSystem.RemoveDevice(_touchscreen);
                _touchscreen = null;
            }

            PressedKeys.Clear();
            _gamepadState = default;
            _mouseState = default;
            if (_driver != null)
            {
                Object.Destroy(_driver.gameObject);
                _driver = null;
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private sealed class InputInjectorDriver : MonoBehaviour
        {
        }

        private static IEnumerator PressCoroutine(GamepadButton button)
        {
            var gamepad = EnsureGamepad();
            SetGamepadButton(button, true);
            InputSystem.QueueStateEvent(gamepad, _gamepadState);
            InputSystem.Update();
            yield return null;
            SetGamepadButton(button, false);
            InputSystem.QueueStateEvent(gamepad, _gamepadState);
            InputSystem.Update();
        }

        private static IEnumerator KeyCoroutine(Key key)
        {
            var keyboard = EnsureKeyboard();
            PressedKeys.Add(key);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(ToPressedKeysArray()));
            InputSystem.Update();
            yield return null;
            PressedKeys.Remove(key);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(ToPressedKeysArray()));
            InputSystem.Update();
        }

        private static IEnumerator ClickCoroutine(Vector2 screenPosition, PointerButton button)
        {
            var mouse = EnsureMouse();
            PointerMove(screenPosition);
            SetMouseButton(button, true);
            InputSystem.QueueStateEvent(mouse, _mouseState);
            InputSystem.Update();
            if (DefaultClickFrameDelaySeconds > 0.0f)
            {
                yield return new WaitForSeconds(DefaultClickFrameDelaySeconds);
            }
            else
            {
                yield return null;
            }

            SetMouseButton(button, false);
            InputSystem.QueueStateEvent(mouse, _mouseState);
            InputSystem.Update();
        }

        private static IEnumerator TapCoroutine(Vector2 screenPosition)
        {
            var touchscreen = EnsureTouchscreen();
            var startTime = Time.realtimeSinceStartupAsDouble;
            QueueTouchState(touchscreen, 1, TouchPhase.Began, screenPosition, Vector2.zero, startTime, screenPosition);
            yield return null;
            QueueTouchState(touchscreen, 1, TouchPhase.Ended, screenPosition, Vector2.zero, startTime, screenPosition);
        }

        private static WaitForSeconds WaitForSeconds(float seconds)
        {
            if (seconds <= 0.0f)
            {
                return new WaitForSeconds(0.0f);
            }

            return new WaitForSeconds(seconds);
        }

        private static void EnsureDriver()
        {
            if (_driver != null)
            {
                return;
            }

            var driverObject = new GameObject(nameof(InputInjector));
            Object.DontDestroyOnLoad(driverObject);
            _driver = driverObject.AddComponent<InputInjectorDriver>();
        }

        private static Gamepad EnsureGamepad()
        {
            if (_gamepad != null)
            {
                return _gamepad;
            }

            _gamepad = InputSystem.AddDevice<Gamepad>("UniLabAI Gamepad");
            return _gamepad;
        }

        private static Keyboard EnsureKeyboard()
        {
            if (_keyboard != null)
            {
                return _keyboard;
            }

            _keyboard = InputSystem.AddDevice<Keyboard>("UniLabAI Keyboard");
            return _keyboard;
        }

        private static Mouse EnsureMouse()
        {
            if (_mouse != null)
            {
                return _mouse;
            }

            _mouse = InputSystem.AddDevice<Mouse>("UniLabAI Mouse");
            _mouseState.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            InputSystem.QueueStateEvent(_mouse, _mouseState);
            InputSystem.Update();
            return _mouse;
        }

        private static Touchscreen EnsureTouchscreen()
        {
            if (_touchscreen != null)
            {
                return _touchscreen;
            }

            _touchscreen = InputSystem.AddDevice<Touchscreen>("UniLabAI Touchscreen");
            return _touchscreen;
        }

        private static void QueueTouchState(Touchscreen touchscreen, int touchId, TouchPhase phase, Vector2 position, Vector2 delta, double startTime, Vector2 startPosition)
        {
            var touchState = new TouchState
            {
                touchId = touchId,
                phase = phase,
                position = position,
                delta = delta,
                pressure = phase == TouchPhase.Ended ? 0.0f : 1.0f,
                startTime = startTime,
                startPosition = startPosition,
            };
            InputSystem.QueueStateEvent(touchscreen, touchState);
            InputSystem.Update();
        }

        private static void SetGamepadButton(GamepadButton button, bool isPressed)
        {
            _gamepadState = _gamepadState.WithButton(button, isPressed);
        }

        private static void SetMouseButton(PointerButton button, bool isPressed)
        {
            var mouseButton = ResolveMouseButton(button);
            _mouseState = _mouseState.WithButton(mouseButton, isPressed);
        }

        private static MouseButton ResolveMouseButton(PointerButton button)
        {
            switch (button)
            {
                case PointerButton.Right:
                    return MouseButton.Right;
                case PointerButton.Middle:
                    return MouseButton.Middle;
                default:
                    return MouseButton.Left;
            }
        }

        private static GamepadButton ResolveDirectionButton(FocusDirection direction)
        {
            switch (direction)
            {
                case FocusDirection.Up:
                    return GamepadButton.DpadUp;
                case FocusDirection.Down:
                    return GamepadButton.DpadDown;
                case FocusDirection.Left:
                    return GamepadButton.DpadLeft;
                case FocusDirection.Right:
                    return GamepadButton.DpadRight;
                default:
                    return GamepadButton.DpadUp;
            }
        }

        private static Key[] ToPressedKeysArray()
        {
            var pressedKeys = new Key[PressedKeys.Count];
            var keyIndex = 0;
            foreach (var key in PressedKeys)
            {
                pressedKeys[keyIndex] = key;
                keyIndex++;
            }

            return pressedKeys;
        }
#endif
    }
}
#endif
