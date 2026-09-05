#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UniLab.AI.InputOverlayVisualPrimitives;

namespace UniLab.AI
{
    /// <summary>ゲームパッド・キーボード・ポインタの図とハイライトを更新します。</summary>
    internal sealed class InputOverlayRenderer
    {
        private readonly GameObject _gameObject;
        private readonly InputOverlayHistory _history = new InputOverlayHistory();
        private readonly InputOverlayInputState _inputState;
        private readonly InputOverlayPointerRenderer _pointer;

        /// <summary>描画ルートと入力状態の通知先を寿命の開始時に固定します。</summary>
        internal InputOverlayRenderer(GameObject gameObject)
        {
            _gameObject = gameObject;
            _inputState = new InputOverlayInputState(_history.AddHistoryEntry, RegisterKeyboardActivity);
            _pointer = new InputOverlayPointerRenderer(_inputState, _history, RegisterKeyboardActivity, IsKeyboardMouseDisplayed);
        }

        /// <summary>機器描画と履歴を従来の順序で更新します。</summary>
        internal void Refresh(float now)
        {
            UpdateDisplayedDevice(now);
            RefreshGamepad(now);
            RefreshKeyboard(now);
            _pointer.RefreshPointer(now);
            _pointer.RefreshTouches(now);
            _history.RefreshHistory(now);
        }

        /// <summary>ステップラベルを履歴へ渡します。</summary>
        internal void AddSyntheticHistory(string label, float now)
        {
            _history.AddSyntheticHistory(label, now);
        }

        /// <summary>キーボード入力を状態保持へ渡します。</summary>
        internal void ReplacePressedKeyboardKeys(List<string> pressedKeys, float now)
        {
            _inputState.ReplacePressedKeyboardKeys(pressedKeys, now);
        }

        /// <summary>入力通知をポインタ描画へ渡します。</summary>
        internal void SetPointerPosition(Vector2 screenPosition, float now)
        {
            _pointer.SetPointerPosition(screenPosition, now);
        }

        /// <summary>入力通知をポインタ描画へ渡します。</summary>
        internal void SetPointerButtons(bool isLeftPressed, bool isRightPressed, bool isMiddlePressed, float now)
        {
            _pointer.SetPointerButtons(isLeftPressed, isRightPressed, isMiddlePressed, now);
        }

        /// <summary>入力通知をポインタ描画へ渡します。</summary>
        internal void ShowScroll(Vector2 delta, float now)
        {
            _pointer.ShowScroll(delta, now);
        }

        /// <summary>入力通知をポインタ描画へ渡します。</summary>
        internal void ReplaceTouches(List<InputOverlayController.TouchSnapshot> touches, float now)
        {
            _pointer.ReplaceTouches(touches, now);
        }

        private void RegisterKeyboardActivity(float now)
        {
            RegisterDeviceActivity(DeviceMode.KeyboardMouse, now);
        }

        private const int OverlaySortingOrder = 32767;
        private const int KeyboardChipLimit = 8;
        private const float GamepadStickRange = 18f;
        private const float DefaultWidgetMargin = 12f;
        private const float DeviceSwitchDelaySeconds = 2f;
        private const float HoldFadeSeconds = 0.2f;
        private const float KeyboardChipWidth = 72f;
        private const float KeyboardChipHeight = 32f;
        private const float GamepadPanelWidth = 340f;
        private const float GamepadPanelHeight = 190f;
        private const float KeyboardPanelWidth = 340f;
        private const float KeyboardPanelHeight = 168f;
        private const float StickCenterThreshold = 0.0001f;
        private static readonly Color WidgetPanelColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color KeyboardPanelColor = new Color(0f, 0f, 0f, 0.38f);
        private static readonly Color IdleChipColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color ActiveChipColor = new Color(0.15f, 0.85f, 0.45f, 0.95f);
        private static readonly Color FadingChipColor = new Color(0.15f, 0.85f, 0.45f, 0.5f);
        private static readonly Color MouseActiveChipColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);
        private static readonly Color MouseFadingChipColor = new Color(0.95f, 0.95f, 0.95f, 0.5f);
        private static readonly Color InactiveLabelColor = new Color(1f, 1f, 1f, 0.72f);
        private static readonly Color GhostStickColor = new Color(0.2f, 0.95f, 0.6f, 0.35f);
        private static readonly Color LiveStickColor = new Color(0.2f, 0.95f, 0.6f, 0.95f);
        private readonly Dictionary<string, ButtonVisualState> _gamepadButtonsByKey = new Dictionary<string, ButtonVisualState>(StringComparer.Ordinal);
        private readonly List<KeyboardChipView> _keyboardChipViews = new List<KeyboardChipView>();
        private readonly List<InputOverlayHeldState> _visibleKeyboardStates = new List<InputOverlayHeldState>();
        private InputOverlayOptions _options;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _rootTransform;
        private RectTransform _gamepadPanel;
        private RectTransform _keyboardPanel;
        private TextMeshProUGUI _keyboardTitle;
        private TextMeshProUGUI _mousePositionLabel;
        private Image _leftStickDot;
        private Image _leftStickGhostDot;
        private Image _rightStickDot;
        private Image _rightStickGhostDot;
        private bool _isInitialized;
        private Vector2 _leftStickValue;
        private Vector2 _rightStickValue;
        private Vector2 _leftStickGhostValue;
        private Vector2 _rightStickGhostValue;
        private float _leftStickGhostReleasedAt;
        private float _rightStickGhostReleasedAt;
        private DeviceMode _displayedDeviceMode;
        private DeviceMode _pendingDeviceMode;
        private float _displayedDeviceLastActivityAt;
        private float _pendingDeviceLastActivityAt;

        /// <summary>
        /// 外部から明示初期化します。
        /// Awake へ依存せず録画開始と同時に確実に表示させるためです。
        /// </summary>
        public void Initialize(InputOverlayOptions options)
        {
            _options = options ?? new InputOverlayOptions();
            _inputState.ApplyOptions(_options);
            _pointer.ApplyOptions(_options);
            if (!_isInitialized)
            {
                BuildVisualTree();
                _isInitialized = true;
            }

            ApplyOptions();
        }

        /// <summary>破棄時に描画参照と履歴を解放します。</summary>
        internal void Clear()
        {
            _keyboardChipViews.Clear();
            _visibleKeyboardStates.Clear();
            _pointer.Clear();
            _history.Clear();
        }

        private void BuildVisualTree()
        {
            InputOverlayHistory.AttachRecordingMarker(_gameObject);

            _canvas = _gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = OverlaySortingOrder;
            _canvas.pixelPerfect = false;

            _canvasGroup = _gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            _rootTransform = _gameObject.GetComponent<RectTransform>();
            if (_rootTransform == null)
            {
                _rootTransform = _gameObject.AddComponent<RectTransform>();
            }

            _rootTransform.anchorMin = Vector2.zero;
            _rootTransform.anchorMax = Vector2.one;
            _rootTransform.offsetMin = Vector2.zero;
            _rootTransform.offsetMax = Vector2.zero;

            var gamepadPanelObject = CreatePanel("GamepadPanel", _rootTransform, new Vector2(GamepadPanelWidth, GamepadPanelHeight), WidgetPanelColor);
            _gamepadPanel = gamepadPanelObject.rectTransform;
            BuildGamepadContents(_gamepadPanel);

            var keyboardPanelObject = CreatePanel("KeyboardPanel", _rootTransform, new Vector2(KeyboardPanelWidth, KeyboardPanelHeight), KeyboardPanelColor);
            _keyboardPanel = keyboardPanelObject.rectTransform;
            BuildKeyboardContents(_keyboardPanel);

            _history.Initialize(_rootTransform, _options);

            _pointer.BuildVisualTree(_rootTransform);
        }

        private void ApplyOptions()
        {
            var scale = Mathf.Max(0.1f, _options.scale);
            _canvasGroup.alpha = Mathf.Clamp01(_options.opacity);
            _gamepadPanel.localScale = Vector3.one * scale;
            _keyboardPanel.localScale = Vector3.one * scale;
            _history.ApplyOptions(_options);

            AnchorToCorner(_gamepadPanel, _options.gamepadCorner, DefaultWidgetMargin, DefaultWidgetMargin);
            AnchorToCorner(_keyboardPanel, _options.gamepadCorner, DefaultWidgetMargin, DefaultWidgetMargin);

            EnsureDisplayedDeviceFallback();
            RefreshStaticSilhouetteVisibility();
        }

        private void UpdateDisplayedDevice(float now)
        {
            if (_pendingDeviceMode == DeviceMode.None || _pendingDeviceMode == _displayedDeviceMode)
            {
                return;
            }

            if (_displayedDeviceMode == DeviceMode.None)
            {
                _displayedDeviceMode = _pendingDeviceMode;
                _displayedDeviceLastActivityAt = _pendingDeviceLastActivityAt;
                RefreshStaticSilhouetteVisibility();
                return;
            }

            if (now - _displayedDeviceLastActivityAt < DeviceSwitchDelaySeconds)
            {
                return;
            }

            _displayedDeviceMode = _pendingDeviceMode;
            _displayedDeviceLastActivityAt = _pendingDeviceLastActivityAt;
            RefreshStaticSilhouetteVisibility();
        }

        private void RefreshGamepad(float now)
        {
            var shouldShow = _options.showGamepad && IsGamepadDisplayed();
            _gamepadPanel.gameObject.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            foreach (var pair in _gamepadButtonsByKey)
            {
                var visualState = pair.Value;
                UpdateButtonVisual(visualState.text, visualState.background, visualState.state, ActiveChipColor, FadingChipColor, now, _options.holdSeconds);
            }

            RefreshStick(_leftStickDot, _leftStickGhostDot, _leftStickValue, _leftStickGhostValue, _leftStickGhostReleasedAt, GamepadStickRange, now);
            RefreshStick(_rightStickDot, _rightStickGhostDot, _rightStickValue, _rightStickGhostValue, _rightStickGhostReleasedAt, GamepadStickRange, now);
        }

        private void RefreshKeyboard(float now)
        {
            var shouldShow = (_options.showKeyboard || _options.showPointer) && IsKeyboardMouseDisplayed();
            _keyboardPanel.gameObject.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            _keyboardTitle.text = _options.showKeyboard ? "Keyboard" : "Mouse";
            _visibleKeyboardStates.Clear();
            if (_options.showKeyboard)
            {
                foreach (var pair in _inputState.KeyboardStatesByKey)
                {
                    if (pair.Value.IsVisible(now, _options.holdSeconds))
                    {
                        _visibleKeyboardStates.Add(pair.Value);
                    }
                }
            }

            _visibleKeyboardStates.Sort(InputOverlayHeldStateComparer.Instance);
            for (var chipIndex = 0; chipIndex < _keyboardChipViews.Count; chipIndex++)
            {
                var chipView = _keyboardChipViews[chipIndex];
                var isActive = chipIndex < _visibleKeyboardStates.Count;
                chipView.root.gameObject.SetActive(isActive);
                if (!isActive)
                {
                    continue;
                }

                var state = _visibleKeyboardStates[chipIndex];
                chipView.label.text = state.GetDisplayText();
                UpdateButtonVisual(chipView.label, chipView.background, state, ActiveChipColor, FadingChipColor, now, _options.holdSeconds);
            }

            RefreshMouseButtonVisual(_pointer.LeftMouseButtonState, now);
            RefreshMouseButtonVisual(_pointer.RightMouseButtonState, now);
            RefreshMouseButtonVisual(_pointer.MiddleMouseButtonState, now);
            _mousePositionLabel.text = $"x {_pointer.PointerPosition.x:0}  y {_pointer.PointerPosition.y:0}";
        }

        /// <summary>
        /// パッドボタン状態を更新します。
        /// 押下と解放の境界をここで持ち、保持表示と履歴追加を同じ判定で扱うためです。
        /// </summary>
        public void UpdateGamepadButtonState(string buttonKey, bool isPressed, float now)
        {
            if (string.IsNullOrEmpty(buttonKey))
            {
                return;
            }

            if (!_gamepadButtonsByKey.TryGetValue(buttonKey, out var visualState))
            {
                return;
            }

            if (isPressed || visualState.state.isPressed)
            {
                RegisterDeviceActivity(DeviceMode.Gamepad, now);
            }

            _inputState.UpdateHeldState(visualState.state, isPressed, now, GetGamepadHistoryLabel(buttonKey), true);
        }

        /// <summary>
        /// パッドスティック位置を更新します。
        /// スティックは方向と倒し量が主体のため、ボタンと別の保持ロジックで扱います。
        /// </summary>
        public void SetGamepadSticks(Vector2 leftStick, Vector2 rightStick, float now)
        {
            if (leftStick.sqrMagnitude > StickCenterThreshold || rightStick.sqrMagnitude > StickCenterThreshold)
            {
                RegisterDeviceActivity(DeviceMode.Gamepad, now);
            }

            _inputState.UpdateStickState(ref _leftStickValue, ref _leftStickGhostValue, ref _leftStickGhostReleasedAt, leftStick, now);
            _inputState.UpdateStickState(ref _rightStickValue, ref _rightStickGhostValue, ref _rightStickGhostReleasedAt, rightStick, now);
        }

        private void BuildGamepadContents(RectTransform panel)
        {
            CreateGamepadChip("LB", panel, new Vector2(54f, -28f), new Vector2(52f, 28f));
            CreateGamepadChip("RB", panel, new Vector2(286f, -28f), new Vector2(52f, 28f));
            CreateGamepadChip("Up", panel, new Vector2(60f, -68f), new Vector2(36f, 36f), "^");
            CreateGamepadChip("Left", panel, new Vector2(34f, -94f), new Vector2(36f, 36f), "<");
            CreateGamepadChip("Right", panel, new Vector2(86f, -94f), new Vector2(36f, 36f), ">");
            CreateGamepadChip("Down", panel, new Vector2(60f, -120f), new Vector2(36f, 36f), "v");
            CreateGamepadChip("X", panel, new Vector2(236f, -84f), new Vector2(40f, 40f));
            CreateGamepadChip("Y", panel, new Vector2(262f, -58f), new Vector2(40f, 40f));
            CreateGamepadChip("A", panel, new Vector2(262f, -110f), new Vector2(40f, 40f));
            CreateGamepadChip("B", panel, new Vector2(288f, -84f), new Vector2(40f, 40f));
            CreateGamepadChip("Select", panel, new Vector2(146f, -118f), new Vector2(68f, 28f), "SEL");
            CreateGamepadChip("Start", panel, new Vector2(218f, -118f), new Vector2(68f, 28f), "START");

            CreateStickDisplay("LS", panel, new Vector2(100f, -150f), GamepadStickRange, out _leftStickDot, out _leftStickGhostDot);
            CreateStickDisplay("RS", panel, new Vector2(240f, -150f), GamepadStickRange, out _rightStickDot, out _rightStickGhostDot);
        }

        private void BuildKeyboardContents(RectTransform panel)
        {
            _keyboardTitle = CreateText("KeyboardTitle", panel, 18, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _keyboardTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _keyboardTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            _keyboardTitle.rectTransform.pivot = new Vector2(0f, 1f);
            _keyboardTitle.rectTransform.sizeDelta = new Vector2(140f, 24f);
            _keyboardTitle.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            _keyboardTitle.text = "Keyboard";

            for (var chipIndex = 0; chipIndex < KeyboardChipLimit; chipIndex++)
            {
                var chipObject = CreatePanel($"KeyChip{chipIndex}", panel, new Vector2(KeyboardChipWidth, KeyboardChipHeight), IdleChipColor);
                chipObject.rectTransform.anchorMin = new Vector2(0f, 1f);
                chipObject.rectTransform.anchorMax = new Vector2(0f, 1f);
                chipObject.rectTransform.pivot = new Vector2(0f, 1f);
                var rowIndex = chipIndex / 4;
                var columnIndex = chipIndex % 4;
                chipObject.rectTransform.anchoredPosition = new Vector2(14f + (columnIndex * (KeyboardChipWidth + 8f)), -38f - (rowIndex * (KeyboardChipHeight + 8f)));
                var label = CreateText($"KeyChipLabel{chipIndex}", chipObject.rectTransform, 18, TextAlignmentOptions.Center, FontStyles.Bold);
                label.overflowMode = TextOverflowModes.Ellipsis;
                Stretch(label.rectTransform, new Vector2(6f, 4f), new Vector2(-6f, -4f));
                chipObject.gameObject.SetActive(false);

                _keyboardChipViews.Add(new KeyboardChipView
                {
                    root = chipObject.rectTransform,
                    background = chipObject,
                    label = label,
                });
            }

            var mouseLabel = CreateText("MouseTitle", panel, 18, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            mouseLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            mouseLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            mouseLabel.rectTransform.pivot = new Vector2(0f, 1f);
            mouseLabel.rectTransform.sizeDelta = new Vector2(120f, 24f);
            mouseLabel.rectTransform.anchoredPosition = new Vector2(14f, -116f);
            mouseLabel.text = "Mouse";

            CreatePointerButtonChip(_pointer.LeftMouseButtonState, panel, "MouseLeftChip", "L", new Vector2(14f, -144f));
            CreatePointerButtonChip(_pointer.RightMouseButtonState, panel, "MouseRightChip", "R", new Vector2(70f, -144f));
            CreatePointerButtonChip(_pointer.MiddleMouseButtonState, panel, "MouseMiddleChip", "M", new Vector2(126f, -144f));

            _mousePositionLabel = CreateText("MousePosition", panel, 16, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            _mousePositionLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _mousePositionLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            _mousePositionLabel.rectTransform.pivot = new Vector2(0f, 1f);
            _mousePositionLabel.rectTransform.sizeDelta = new Vector2(170f, 22f);
            _mousePositionLabel.rectTransform.anchoredPosition = new Vector2(190f, -145f);
            _mousePositionLabel.text = "x 0  y 0";
        }

        private void CreateStickDisplay(string label, RectTransform parent, Vector2 position, float range, out Image dot, out Image ghostDot)
        {
            var stickRoot = CreateContainer($"{label}Root", parent);
            stickRoot.anchorMin = new Vector2(0f, 1f);
            stickRoot.anchorMax = new Vector2(0f, 1f);
            stickRoot.pivot = new Vector2(0.5f, 0.5f);
            stickRoot.anchoredPosition = position;

            var ring = CreateImage($"{label}Ring", stickRoot, GetCircleTexture(), new Color(1f, 1f, 1f, 0.2f));
            ring.rectTransform.sizeDelta = new Vector2((range * 2f) + 14f, (range * 2f) + 14f);

            ghostDot = CreateImage($"{label}GhostDot", stickRoot, GetCircleTexture(), GhostStickColor);
            ghostDot.rectTransform.sizeDelta = new Vector2(12f, 12f);
            ghostDot.gameObject.SetActive(false);

            dot = CreateImage($"{label}Dot", stickRoot, GetCircleTexture(), LiveStickColor);
            dot.rectTransform.sizeDelta = new Vector2(12f, 12f);

            var text = CreateText($"{label}Text", stickRoot, 16, TextAlignmentOptions.Center, FontStyles.Bold);
            text.text = label;
            text.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            text.rectTransform.sizeDelta = new Vector2(60f, 20f);
        }

        private void CreateGamepadChip(string key, RectTransform parent, Vector2 position, Vector2 size)
        {
            CreateGamepadChip(key, parent, position, size, key);
        }

        private void CreateGamepadChip(string key, RectTransform parent, Vector2 position, Vector2 size, string label)
        {
            var chip = CreatePanel($"{key}Chip", parent, size, IdleChipColor);
            chip.rectTransform.anchorMin = new Vector2(0f, 1f);
            chip.rectTransform.anchorMax = new Vector2(0f, 1f);
            chip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            chip.rectTransform.anchoredPosition = position;
            var text = CreateText($"{key}Label", chip.rectTransform, 18, TextAlignmentOptions.Center, FontStyles.Bold);
            text.text = label;
            Stretch(text.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            _gamepadButtonsByKey[key] = new ButtonVisualState(chip, text, new InputOverlayHeldState(label));
        }

        private void CreatePointerButtonChip(InputOverlayHeldState state, RectTransform parent, string name, string label, Vector2 position)
        {
            var chip = CreatePanel(name, parent, new Vector2(48f, 28f), IdleChipColor);
            chip.rectTransform.anchorMin = new Vector2(0f, 1f);
            chip.rectTransform.anchorMax = new Vector2(0f, 1f);
            chip.rectTransform.pivot = new Vector2(0f, 1f);
            chip.rectTransform.anchoredPosition = position;
            var text = CreateText($"{name}Label", chip.rectTransform, 16, TextAlignmentOptions.Center, FontStyles.Bold);
            text.text = label;
            Stretch(text.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            state.BindVisual(chip, text);
        }

        private static void UpdateButtonVisual(TextMeshProUGUI label, Image background, InputOverlayHeldState state, Color activeColor, Color fadingColor, float now, float holdSeconds)
        {
            if (label == null || background == null || state == null)
            {
                return;
            }

            var alpha = state.GetAlpha(now, holdSeconds);
            if (state.isPressed)
            {
                background.color = activeColor;
                label.color = Color.white;
                return;
            }

            if (alpha > 0f)
            {
                background.color = Color.Lerp(IdleChipColor, fadingColor, alpha);
                label.color = Color.Lerp(InactiveLabelColor, Color.white, alpha);
                return;
            }

            background.color = IdleChipColor;
            label.color = InactiveLabelColor;
        }

        private void RefreshMouseButtonVisual(InputOverlayHeldState state, float now)
        {
            if (state.label == null || state.background == null)
            {
                return;
            }

            state.label.text = state.GetDisplayText();
            UpdateButtonVisual(state.label, state.background, state, MouseActiveChipColor, MouseFadingChipColor, now, _options.holdSeconds);
        }

        private void RefreshStick(Image liveDot, Image ghostDot, Vector2 liveValue, Vector2 ghostValue, float ghostReleasedAt, float range, float now)
        {
            liveDot.rectTransform.anchoredPosition = liveValue * range;
            var hasLiveValue = liveValue.sqrMagnitude > StickCenterThreshold;
            liveDot.color = hasLiveValue ? LiveStickColor : new Color(LiveStickColor.r, LiveStickColor.g, LiveStickColor.b, 0.25f);
            if (hasLiveValue)
            {
                ghostDot.gameObject.SetActive(false);
                return;
            }

            var ghostAlpha = GetGhostAlpha(ghostReleasedAt, now);
            if (ghostAlpha <= 0f || ghostValue.sqrMagnitude <= StickCenterThreshold)
            {
                ghostDot.gameObject.SetActive(false);
                return;
            }

            ghostDot.gameObject.SetActive(true);
            ghostDot.rectTransform.anchoredPosition = ghostValue * range;
            ghostDot.color = new Color(GhostStickColor.r, GhostStickColor.g, GhostStickColor.b, ghostAlpha);
        }

        private float GetGhostAlpha(float releasedAt, float now)
        {
            if (releasedAt <= 0f)
            {
                return 0f;
            }

            var holdSeconds = Mathf.Max(0.01f, _options.holdSeconds);
            var elapsed = now - releasedAt;
            if (elapsed <= holdSeconds)
            {
                return 1f;
            }

            var fadeElapsed = elapsed - holdSeconds;
            if (fadeElapsed >= HoldFadeSeconds)
            {
                return 0f;
            }

            return 1f - (fadeElapsed / HoldFadeSeconds);
        }

        private void RegisterDeviceActivity(DeviceMode deviceMode, float now)
        {
            if (deviceMode == DeviceMode.None)
            {
                return;
            }

            if (_displayedDeviceMode == deviceMode)
            {
                _displayedDeviceLastActivityAt = now;
                _pendingDeviceMode = DeviceMode.None;
                _pendingDeviceLastActivityAt = 0f;
                return;
            }

            _pendingDeviceMode = deviceMode;
            _pendingDeviceLastActivityAt = now;
        }

        private void EnsureDisplayedDeviceFallback()
        {
            if (_displayedDeviceMode == DeviceMode.Gamepad && !_options.showGamepad)
            {
                _displayedDeviceMode = DeviceMode.None;
            }

            if (_displayedDeviceMode == DeviceMode.KeyboardMouse && !_options.showKeyboard && !_options.showPointer)
            {
                _displayedDeviceMode = DeviceMode.None;
            }

            if (_displayedDeviceMode != DeviceMode.None)
            {
                return;
            }

            if (_options.showGamepad)
            {
                _displayedDeviceMode = DeviceMode.Gamepad;
                return;
            }

            if (_options.showKeyboard || _options.showPointer)
            {
                _displayedDeviceMode = DeviceMode.KeyboardMouse;
            }
        }

        private void RefreshStaticSilhouetteVisibility()
        {
            _gamepadPanel.gameObject.SetActive(_options.showGamepad && IsGamepadDisplayed());
            _keyboardPanel.gameObject.SetActive((_options.showKeyboard || _options.showPointer) && IsKeyboardMouseDisplayed());
        }

        private bool IsGamepadDisplayed()
        {
            if (!_options.alwaysShowSilhouette)
            {
                return HasAnyGamepadHighlight(Time.realtimeSinceStartup);
            }

            return _displayedDeviceMode == DeviceMode.Gamepad;
        }

        private bool IsKeyboardMouseDisplayed()
        {
            if (!_options.alwaysShowSilhouette)
            {
                return HasAnyKeyboardMouseHighlight(Time.realtimeSinceStartup);
            }

            return _displayedDeviceMode == DeviceMode.KeyboardMouse;
        }

        private bool HasAnyGamepadHighlight(float now)
        {
            foreach (var pair in _gamepadButtonsByKey)
            {
                if (pair.Value.state.IsVisible(now, _options.holdSeconds))
                {
                    return true;
                }
            }

            return _leftStickValue.sqrMagnitude > StickCenterThreshold
                || _rightStickValue.sqrMagnitude > StickCenterThreshold
                || GetGhostAlpha(_leftStickGhostReleasedAt, now) > 0f
                || GetGhostAlpha(_rightStickGhostReleasedAt, now) > 0f;
        }

        private bool HasAnyKeyboardMouseHighlight(float now)
        {
            foreach (var pair in _inputState.KeyboardStatesByKey)
            {
                if (pair.Value.IsVisible(now, _options.holdSeconds))
                {
                    return true;
                }
            }

            return _pointer.LeftMouseButtonState.IsVisible(now, _options.holdSeconds)
                || _pointer.RightMouseButtonState.IsVisible(now, _options.holdSeconds)
                || _pointer.MiddleMouseButtonState.IsVisible(now, _options.holdSeconds);
        }

        private static string GetGamepadHistoryLabel(string buttonKey)
        {
            switch (buttonKey)
            {
                case "Up":
                    return "↑";
                case "Down":
                    return "↓";
                case "Left":
                    return "←";
                case "Right":
                    return "→";
                default:
                    return buttonKey;
            }
        }

        private enum DeviceMode
        {
            None = 0,
            Gamepad = 1,
            KeyboardMouse = 2,
        }

        private sealed class ButtonVisualState
        {
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public ButtonVisualState(Image background, TextMeshProUGUI text, InputOverlayHeldState state)
            {
                this.background = background;
                this.text = text;
                this.state = state;
            }

            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public readonly Image background;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public readonly TextMeshProUGUI text;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public readonly InputOverlayHeldState state;
        }

        private sealed class InputOverlayHeldStateComparer : IComparer<InputOverlayHeldState>
        {
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public static readonly InputOverlayHeldStateComparer Instance = new InputOverlayHeldStateComparer();

            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public int Compare(InputOverlayHeldState x, InputOverlayHeldState y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x == null)
                {
                    return 1;
                }

                if (y == null)
                {
                    return -1;
                }

                return y.lastVisibleAt.CompareTo(x.lastVisibleAt);
            }
        }

        private struct KeyboardChipView
        {
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public RectTransform root;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public Image background;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public TextMeshProUGUI label;
        }

    }
}
#endif
