#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AI
{
    /// <summary>
    /// 入力状態を ScreenSpaceOverlay の UI として描画します。
    /// 録画へ自然に写るオーバーレイとして、常時シルエットと直前入力の両方を読める形に保ちます。
    /// </summary>
    public sealed class InputOverlayController : MonoBehaviour
    {
        private const int OverlaySortingOrder = 32767;
        private const int DragTrailSegmentLimit = 24;
        private const int KeyboardChipLimit = 8;
        private const float PointerRingDurationSeconds = 0.35f;
        private const float DragTrailFadeSeconds = 0.5f;
        private const float ScrollIndicatorDurationSeconds = 0.2f;
        private const float PointerMoveThreshold = 2f;
        private const float GamepadStickRange = 18f;
        private const float DefaultWidgetMargin = 12f;
        private const float DeviceSwitchDelaySeconds = 2f;
        private const float HoldFadeSeconds = 0.2f;
        private const float KeyboardChipWidth = 72f;
        private const float KeyboardChipHeight = 32f;
        private const float HistoryItemWidth = 164f;
        private const float HistoryItemHeight = 56f;
        private const float GamepadPanelWidth = 340f;
        private const float GamepadPanelHeight = 190f;
        private const float KeyboardPanelWidth = 340f;
        private const float KeyboardPanelHeight = 168f;
        private const float HistoryPanelHeight = 56f;
        private const float HistoryPanelSpacing = 8f;
        private const float HistoryPanelMaxWidthRatio = 0.4f;
        private const float HistoryItemHorizontalPadding = 10f;
        private const float HistoryItemVerticalPadding = 6f;
        private const float HistoryItemMinimumWidth = 56f;
        private const float HistorySeparatorWidth = 16f;
        private const float TouchDiameter = 56f;
        private const float StickCenterThreshold = 0.0001f;
        private static readonly Color WidgetPanelColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color KeyboardPanelColor = new Color(0f, 0f, 0f, 0.38f);
        private static readonly Color HistoryPanelColor = new Color(0f, 0f, 0f, 0f);
        private static readonly Color IdleChipColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color ActiveChipColor = new Color(0.15f, 0.85f, 0.45f, 0.95f);
        private static readonly Color FadingChipColor = new Color(0.15f, 0.85f, 0.45f, 0.5f);
        private static readonly Color MouseActiveChipColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);
        private static readonly Color MouseFadingChipColor = new Color(0.95f, 0.95f, 0.95f, 0.5f);
        private static readonly Color InactiveLabelColor = new Color(1f, 1f, 1f, 0.72f);
        private static readonly Color GhostStickColor = new Color(0.2f, 0.95f, 0.6f, 0.35f);
        private static readonly Color LiveStickColor = new Color(0.2f, 0.95f, 0.6f, 0.95f);
        private static readonly Color PointerIdleColor = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color PointerActiveColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color HistoryTextColor = ParseHtmlColor("#F5F0E6");
        private static readonly Color HistoryTimeColor = new Color(0.9607843f, 0.9411765f, 0.9019608f, 0.62f);
        private static readonly Color HistoryItemBackgroundColor = ParseHtmlColor("#00000099");
        private const string WhiteColorHtml = "#FFFFFF";
        private const string YellowColorHtml = "#FFE16A";
        private const string BlueColorHtml = "#79C9FF";

        private static Texture2D s_circleTexture;
        private static Sprite s_whiteSprite;
        private static Sprite s_circleSprite;

        private readonly Dictionary<string, ButtonVisualState> _gamepadButtonsByKey = new Dictionary<string, ButtonVisualState>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeldInputState> _keyboardStatesByKey = new Dictionary<string, HeldInputState>(StringComparer.Ordinal);
        private readonly List<KeyboardChipView> _keyboardChipViews = new List<KeyboardChipView>();
        private readonly List<HeldInputState> _visibleKeyboardStates = new List<HeldInputState>();
        private readonly List<PointerTrailPoint> _pointerTrailPoints = new List<PointerTrailPoint>();
        private readonly List<Image> _pointerTrailSegments = new List<Image>();
        private readonly List<PointerClickPulse> _pointerClickPulses = new List<PointerClickPulse>();
        private readonly List<Image> _pointerClickPulseViews = new List<Image>();
        private readonly Dictionary<int, TouchView> _touchViewsById = new Dictionary<int, TouchView>();
        private readonly List<int> _releasedTouchIds = new List<int>();
        private readonly List<HistoryEntry> _historyEntries = new List<HistoryEntry>();
        private readonly List<HistoryItemView> _historyItemViews = new List<HistoryItemView>();
        private readonly List<TextMeshProUGUI> _historySeparatorViews = new List<TextMeshProUGUI>();
        private readonly LegacyInputProxy _legacyInputProxy = new LegacyInputProxy();
        private readonly InputSystemProxy _inputSystemProxy = new InputSystemProxy();

        private InputOverlayOptions _options;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _rootTransform;
        private RectTransform _gamepadPanel;
        private RectTransform _keyboardPanel;
        private RectTransform _pointerLayer;
        private RectTransform _touchLayer;
        private RectTransform _historyPanel;
        private RectTransform _pointerRoot;
        private TextMeshProUGUI _scrollIndicator;
        private TextMeshProUGUI _keyboardTitle;
        private TextMeshProUGUI _mousePositionLabel;
        private Image _leftStickDot;
        private Image _leftStickGhostDot;
        private Image _rightStickDot;
        private Image _rightStickGhostDot;
        private Image _pointerShaft;
        private Image _pointerWingTop;
        private Image _pointerWingBottom;
        private HeldInputState _leftMouseButtonState;
        private HeldInputState _rightMouseButtonState;
        private HeldInputState _middleMouseButtonState;
        private bool _isInitialized;
        private Vector2 _previousPointerPosition;
        private Vector2 _pointerPosition;
        private Vector2 _leftStickValue;
        private Vector2 _rightStickValue;
        private Vector2 _leftStickGhostValue;
        private Vector2 _rightStickGhostValue;
        private float _leftStickGhostReleasedAt;
        private float _rightStickGhostReleasedAt;
        private float _scrollIndicatorVisibleUntil;
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
            if (!_isInitialized)
            {
                BuildVisualTree();
                _isInitialized = true;
            }

            ApplyOptions();
        }

        /// <summary>
        /// 疑似操作のラベルを履歴帯へ追加します。
        /// submit のような実入力を伴わない操作も動画から読み返せるようにするためです。
        /// </summary>
        public void AddSyntheticHistory(string label, float now)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            AddHistoryEntry(label, now);
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            PollInput(now);
            UpdateDisplayedDevice(now);
            RefreshGamepad(now);
            RefreshKeyboard(now);
            RefreshPointer(now);
            RefreshTouches(now);
            RefreshHistory(now);
        }

        private void OnDestroy()
        {
            _keyboardChipViews.Clear();
            _visibleKeyboardStates.Clear();
            _pointerTrailSegments.Clear();
            _pointerClickPulseViews.Clear();
            _touchViewsById.Clear();
            _historyItemViews.Clear();
            _historySeparatorViews.Clear();
            _historyEntries.Clear();
        }

        private void BuildVisualTree()
        {
            gameObject.AddComponent<UiOverlayMarker>();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = OverlaySortingOrder;
            _canvas.pixelPerfect = false;

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            _rootTransform = gameObject.GetComponent<RectTransform>();
            if (_rootTransform == null)
            {
                _rootTransform = gameObject.AddComponent<RectTransform>();
            }

            _rootTransform.anchorMin = Vector2.zero;
            _rootTransform.anchorMax = Vector2.one;
            _rootTransform.offsetMin = Vector2.zero;
            _rootTransform.offsetMax = Vector2.zero;

            _leftMouseButtonState = CreatePointerButtonState("L");
            _rightMouseButtonState = CreatePointerButtonState("R");
            _middleMouseButtonState = CreatePointerButtonState("M");

            var gamepadPanelObject = CreatePanel("GamepadPanel", _rootTransform, new Vector2(GamepadPanelWidth, GamepadPanelHeight), WidgetPanelColor);
            _gamepadPanel = gamepadPanelObject.rectTransform;
            BuildGamepadContents(_gamepadPanel);

            var keyboardPanelObject = CreatePanel("KeyboardPanel", _rootTransform, new Vector2(KeyboardPanelWidth, KeyboardPanelHeight), KeyboardPanelColor);
            _keyboardPanel = keyboardPanelObject.rectTransform;
            BuildKeyboardContents(_keyboardPanel);

            var historyPanelObject = CreatePanel("HistoryPanel", _rootTransform, new Vector2(0f, HistoryPanelHeight), HistoryPanelColor);
            _historyPanel = historyPanelObject.rectTransform;
            BuildHistoryContents(_historyPanel);

            _pointerLayer = CreateContainer("PointerLayer", _rootTransform);
            BuildPointerContents(_pointerLayer);

            _touchLayer = CreateContainer("TouchLayer", _rootTransform);
        }

        private void ApplyOptions()
        {
            var scale = Mathf.Max(0.1f, _options.scale);
            _canvasGroup.alpha = Mathf.Clamp01(_options.opacity);
            _gamepadPanel.localScale = Vector3.one * scale;
            _keyboardPanel.localScale = Vector3.one * scale;
            _historyPanel.localScale = Vector3.one * scale;

            AnchorToCorner(_gamepadPanel, _options.gamepadCorner, DefaultWidgetMargin, DefaultWidgetMargin);
            AnchorToCorner(_keyboardPanel, _options.gamepadCorner, DefaultWidgetMargin, DefaultWidgetMargin);
            AnchorHistoryPanel(_historyPanel, _options.historyCorner);

            EnsureDisplayedDeviceFallback();
            RefreshStaticSilhouetteVisibility();
        }

        private void PollInput(float now)
        {
            if (_inputSystemProxy.TryPoll(this, now))
            {
                return;
            }

            _legacyInputProxy.Poll(this, now);
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
                foreach (var pair in _keyboardStatesByKey)
                {
                    if (pair.Value.IsVisible(now, _options.holdSeconds))
                    {
                        _visibleKeyboardStates.Add(pair.Value);
                    }
                }
            }

            _visibleKeyboardStates.Sort(HeldInputStateComparer.Instance);
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

            RefreshMouseButtonVisual(_leftMouseButtonState, now);
            RefreshMouseButtonVisual(_rightMouseButtonState, now);
            RefreshMouseButtonVisual(_middleMouseButtonState, now);
            _mousePositionLabel.text = $"x {_pointerPosition.x:0}  y {_pointerPosition.y:0}";
        }

        private void RefreshPointer(float now)
        {
            var shouldShow = _options.showPointer && IsKeyboardMouseDisplayed();
            _pointerRoot.gameObject.SetActive(shouldShow);
            if (shouldShow)
            {
                var pointerColor = IsAnyPointerButtonActive(now) ? PointerActiveColor : PointerIdleColor;
                _pointerShaft.color = pointerColor;
                _pointerWingTop.color = pointerColor;
                _pointerWingBottom.color = pointerColor;
            }

            for (var pulseIndex = _pointerClickPulses.Count - 1; pulseIndex >= 0; pulseIndex--)
            {
                var pulse = _pointerClickPulses[pulseIndex];
                var elapsed = now - pulse.startedAt;
                if (elapsed > PointerRingDurationSeconds || !shouldShow)
                {
                    _pointerClickPulseViews[pulseIndex].gameObject.SetActive(false);
                    _pointerClickPulses.RemoveAt(pulseIndex);
                    _pointerClickPulseViews.RemoveAt(pulseIndex);
                    continue;
                }

                var view = _pointerClickPulseViews[pulseIndex];
                var progress = elapsed / PointerRingDurationSeconds;
                var alpha = 1f - progress;
                view.color = new Color(pulse.color.r, pulse.color.g, pulse.color.b, alpha);
                var size = Mathf.Lerp(12f, 52f, progress);
                view.rectTransform.sizeDelta = new Vector2(size, size);
                view.rectTransform.anchoredPosition = pulse.position;
                view.gameObject.SetActive(true);
            }

            for (var segmentIndex = _pointerTrailSegments.Count - 1; segmentIndex >= 0; segmentIndex--)
            {
                _pointerTrailSegments[segmentIndex].gameObject.SetActive(false);
            }

            if (!shouldShow)
            {
                _pointerTrailPoints.Clear();
                _scrollIndicator.gameObject.SetActive(false);
                return;
            }

            var visibleSegmentCount = 0;
            for (var pointIndex = _pointerTrailPoints.Count - 1; pointIndex >= 1; pointIndex--)
            {
                var newerPoint = _pointerTrailPoints[pointIndex];
                var olderPoint = _pointerTrailPoints[pointIndex - 1];
                var age = now - newerPoint.recordedAt;
                if (age > DragTrailFadeSeconds)
                {
                    _pointerTrailPoints.RemoveAt(pointIndex);
                    continue;
                }

                if (visibleSegmentCount >= DragTrailSegmentLimit)
                {
                    continue;
                }

                var segment = EnsureTrailSegment(visibleSegmentCount);
                ConfigureTrailSegment(segment, olderPoint.position, newerPoint.position, 1f - (age / DragTrailFadeSeconds));
                visibleSegmentCount++;
            }

            if (_pointerTrailPoints.Count == 1 && now - _pointerTrailPoints[0].recordedAt > DragTrailFadeSeconds)
            {
                _pointerTrailPoints.Clear();
            }

            _scrollIndicator.gameObject.SetActive(_scrollIndicatorVisibleUntil >= now);
        }

        private void RefreshTouches(float now)
        {
            if (!_options.showTouch)
            {
                return;
            }

            foreach (var releasedTouchId in _releasedTouchIds)
            {
                if (!_touchViewsById.TryGetValue(releasedTouchId, out var touchView))
                {
                    continue;
                }

                Destroy(touchView.root.gameObject);
                _touchViewsById.Remove(releasedTouchId);
            }

            _releasedTouchIds.Clear();
        }

        private void RefreshHistory(float now)
        {
            var visibleCount = Mathf.Min(Mathf.Max(0, _options.historyCount), Mathf.Min(_historyEntries.Count, _historyItemViews.Count));
            _historyPanel.gameObject.SetActive(visibleCount > 0);
            if (visibleCount <= 0)
            {
                HideAllHistoryViews();
                return;
            }

            var historyScale = Mathf.Max(0.1f, _options.scale);
            var maxPanelWidth = (Screen.width * HistoryPanelMaxWidthRatio) / historyScale;
            var visibleHistoryStartIndex = _historyEntries.Count - visibleCount;
            var itemWidths = new float[visibleCount];
            var preferredItemWidthTotal = 0f;
            for (var visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
            {
                var itemView = _historyItemViews[visibleIndex];
                var historyEntry = _historyEntries[visibleHistoryStartIndex + visibleIndex];
                EnsureHistoryTextStyle(itemView.label, HistoryTextColor);
                EnsureHistoryTextStyle(itemView.elapsed, HistoryTimeColor);
                itemView.label.text = historyEntry.label;
                itemView.label.ForceMeshUpdate();
                itemView.elapsed.text = historyEntry.cachedElapsedText;
                itemView.elapsed.ForceMeshUpdate();

                var preferredWidth = Mathf.Max(itemView.label.preferredWidth, itemView.elapsed.preferredWidth) + (HistoryItemHorizontalPadding * 2f);
                itemWidths[visibleIndex] = Mathf.Max(HistoryItemMinimumWidth, preferredWidth);
                preferredItemWidthTotal += itemWidths[visibleIndex];
            }

            var separatorCount = Mathf.Max(0, visibleCount - 1);
            var separatorWidthTotal = separatorCount * HistorySeparatorWidth;
            var allowedItemWidthTotal = Mathf.Max(HistoryItemMinimumWidth, maxPanelWidth - separatorWidthTotal);
            if (preferredItemWidthTotal > allowedItemWidthTotal)
            {
                var shrinkRatio = allowedItemWidthTotal / preferredItemWidthTotal;
                for (var visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
                {
                    itemWidths[visibleIndex] = Mathf.Max(HistoryItemMinimumWidth, itemWidths[visibleIndex] * shrinkRatio);
                }
            }

            var contentWidth = separatorWidthTotal;
            for (var visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
            {
                contentWidth += itemWidths[visibleIndex];
            }

            _historyPanel.sizeDelta = new Vector2(Mathf.Min(maxPanelWidth, contentWidth), HistoryPanelHeight);
            AnchorHistoryPanel(_historyPanel, _options.historyCorner);
            var currentX = 0f;

            for (var viewIndex = 0; viewIndex < _historyItemViews.Count; viewIndex++)
            {
                var itemView = _historyItemViews[viewIndex];
                var historyIndex = visibleHistoryStartIndex + viewIndex;
                var isVisible = viewIndex < visibleCount && historyIndex >= 0;
                itemView.root.gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var entry = _historyEntries[historyIndex];
                itemView.label.text = entry.label;

                var elapsedSeconds = Mathf.Max(0f, now - entry.startedAt);
                var elapsedTenths = Mathf.FloorToInt(elapsedSeconds * 10f);
                if (entry.elapsedTenths != elapsedTenths)
                {
                    // perf: 0.1 秒刻みだけ文字列を更新し、毎フレームの GC を避けます。
                    entry.elapsedTenths = elapsedTenths;
                    entry.cachedElapsedText = $"{elapsedTenths * 0.1f:0.0}s";
                    _historyEntries[historyIndex] = entry;
                }

                itemView.elapsed.text = entry.cachedElapsedText;
                ApplyHistoryItemLayout(itemView, itemWidths[viewIndex], currentX);
                currentX += itemWidths[viewIndex];

                if (viewIndex >= _historySeparatorViews.Count)
                {
                    continue;
                }

                var separator = _historySeparatorViews[viewIndex];
                var shouldShowSeparator = viewIndex < visibleCount - 1;
                separator.gameObject.SetActive(shouldShowSeparator);
                if (shouldShowSeparator)
                {
                    EnsureHistoryTextStyle(separator, HistoryTextColor);
                    separator.rectTransform.anchoredPosition = new Vector2(currentX, 0f);
                    separator.rectTransform.sizeDelta = new Vector2(HistorySeparatorWidth, HistoryPanelHeight);
                    currentX += HistorySeparatorWidth;
                }
            }

            for (var separatorIndex = visibleCount - 1; separatorIndex < _historySeparatorViews.Count; separatorIndex++)
            {
                if (separatorIndex < 0)
                {
                    continue;
                }

                _historySeparatorViews[separatorIndex].gameObject.SetActive(false);
            }
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

            UpdateHeldState(visualState.state, isPressed, now, GetGamepadHistoryLabel(buttonKey), true);
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

            UpdateStickState(ref _leftStickValue, ref _leftStickGhostValue, ref _leftStickGhostReleasedAt, leftStick, now);
            UpdateStickState(ref _rightStickValue, ref _rightStickGhostValue, ref _rightStickGhostReleasedAt, rightStick, now);
        }

        /// <summary>
        /// キー押下集合を更新します。
        /// キーごとの立ち上がりを保持して、再押下回数と履歴追加を polling でも再現するためです。
        /// </summary>
        public void ReplacePressedKeyboardKeys(List<string> pressedKeys, float now)
        {
            if (pressedKeys.Count > 0)
            {
                RegisterDeviceActivity(DeviceMode.KeyboardMouse, now);
            }

            for (var keyIndex = 0; keyIndex < pressedKeys.Count; keyIndex++)
            {
                var keyName = pressedKeys[keyIndex];
                if (string.IsNullOrEmpty(keyName))
                {
                    continue;
                }

                if (!_keyboardStatesByKey.TryGetValue(keyName, out var state))
                {
                    state = new HeldInputState(keyName);
                    _keyboardStatesByKey.Add(keyName, state);
                }

                UpdateHeldState(state, true, now, keyName, true);
            }

            foreach (var pair in _keyboardStatesByKey)
            {
                if (pressedKeys.Contains(pair.Key))
                {
                    continue;
                }

                UpdateHeldState(pair.Value, false, now, string.Empty, false);
            }
        }

        /// <summary>
        /// ポインタ位置を更新します。
        /// キーボード＋マウス模式図の利用機器判定と画面上カーソル描画の両方で使うためです。
        /// </summary>
        public void SetPointerPosition(Vector2 screenPosition, float now)
        {
            if (Vector2.Distance(_pointerPosition, screenPosition) >= PointerMoveThreshold)
            {
                RegisterDeviceActivity(DeviceMode.KeyboardMouse, now);
            }

            _pointerPosition = screenPosition;
            _pointerRoot.anchoredPosition = screenPosition;
            _scrollIndicator.rectTransform.anchoredPosition = screenPosition + new Vector2(18f, 24f);
        }

        /// <summary>
        /// ポインタボタン状態を更新します。
        /// クリック波紋、押下保持、履歴追加、ドラッグ軌跡を同じ立ち上がり判定に束ねます。
        /// </summary>
        public void SetPointerButtons(bool isLeftPressed, bool isRightPressed, bool isMiddlePressed, float now)
        {
            if (isLeftPressed || isRightPressed || isMiddlePressed || _leftMouseButtonState.isPressed || _rightMouseButtonState.isPressed || _middleMouseButtonState.isPressed)
            {
                RegisterDeviceActivity(DeviceMode.KeyboardMouse, now);
            }

            UpdatePointerButtonState(_leftMouseButtonState, isLeftPressed, now, ParseHtmlColor(WhiteColorHtml), "LeftClick");
            UpdatePointerButtonState(_rightMouseButtonState, isRightPressed, now, ParseHtmlColor(YellowColorHtml), "RightClick");
            UpdatePointerButtonState(_middleMouseButtonState, isMiddlePressed, now, ParseHtmlColor(BlueColorHtml), "MiddleClick");

            var isAnyPressed = isLeftPressed || isRightPressed || isMiddlePressed;
            var hasMoved = Vector2.Distance(_previousPointerPosition, _pointerRoot.anchoredPosition) >= PointerMoveThreshold;
            if (isAnyPressed && hasMoved)
            {
                _pointerTrailPoints.Add(new PointerTrailPoint
                {
                    position = _pointerRoot.anchoredPosition,
                    recordedAt = now,
                });

                while (_pointerTrailPoints.Count > DragTrailSegmentLimit + 1)
                {
                    _pointerTrailPoints.RemoveAt(0);
                }
            }
            else if (!isAnyPressed)
            {
                for (var pointIndex = 0; pointIndex < _pointerTrailPoints.Count; pointIndex++)
                {
                    var point = _pointerTrailPoints[pointIndex];
                    point.recordedAt = Mathf.Min(point.recordedAt, now);
                    _pointerTrailPoints[pointIndex] = point;
                }
            }

            _previousPointerPosition = _pointerRoot.anchoredPosition;
        }

        /// <summary>
        /// スクロール表示を短時間だけ残します。
        /// マウス移動と違って矢印が無いと録画上で操作を読み取りにくいためです。
        /// </summary>
        public void ShowScroll(Vector2 delta, float now)
        {
            if (Mathf.Abs(delta.y) < 0.01f)
            {
                return;
            }

            RegisterDeviceActivity(DeviceMode.KeyboardMouse, now);
            _scrollIndicator.text = delta.y > 0f ? "^" : "v";
            _scrollIndicatorVisibleUntil = now + ScrollIndicatorDurationSeconds;
        }

        /// <summary>
        /// アクティブなタッチ一覧を反映します。
        /// タップ開始だけ履歴へ残しつつ、描画そのものは接触中の指に限定します。
        /// </summary>
        public void ReplaceTouches(List<TouchSnapshot> touches, float now)
        {
            _releasedTouchIds.Clear();
            foreach (var pair in _touchViewsById)
            {
                _releasedTouchIds.Add(pair.Key);
            }

            for (var touchIndex = 0; touchIndex < touches.Count; touchIndex++)
            {
                var touch = touches[touchIndex];
                if (!_touchViewsById.TryGetValue(touch.touchId, out var touchView))
                {
                    touchView = CreateTouchView(touch.touchId);
                    _touchViewsById.Add(touch.touchId, touchView);
                    AddHistoryEntry("Tap", now);
                }

                touchView.root.anchoredPosition = touch.position;
                touchView.label.text = touch.touchId.ToString();
                touchView.root.gameObject.SetActive(true);
                _releasedTouchIds.Remove(touch.touchId);
            }
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

            CreatePointerButtonChip(_leftMouseButtonState, panel, "MouseLeftChip", "L", new Vector2(14f, -144f));
            CreatePointerButtonChip(_rightMouseButtonState, panel, "MouseRightChip", "R", new Vector2(70f, -144f));
            CreatePointerButtonChip(_middleMouseButtonState, panel, "MouseMiddleChip", "M", new Vector2(126f, -144f));

            _mousePositionLabel = CreateText("MousePosition", panel, 16, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            _mousePositionLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _mousePositionLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            _mousePositionLabel.rectTransform.pivot = new Vector2(0f, 1f);
            _mousePositionLabel.rectTransform.sizeDelta = new Vector2(170f, 22f);
            _mousePositionLabel.rectTransform.anchoredPosition = new Vector2(190f, -145f);
            _mousePositionLabel.text = "x 0  y 0";
        }

        private void BuildHistoryContents(RectTransform panel)
        {
            for (var itemIndex = 0; itemIndex < KeyboardChipLimit; itemIndex++)
            {
                var itemRoot = CreatePanel($"HistoryItem{itemIndex}", panel, new Vector2(HistoryItemWidth, HistoryItemHeight), HistoryItemBackgroundColor);
                itemRoot.rectTransform.anchorMin = new Vector2(0f, 0f);
                itemRoot.rectTransform.anchorMax = new Vector2(0f, 0f);
                itemRoot.rectTransform.pivot = new Vector2(0f, 0f);

                var label = CreateText($"HistoryLabel{itemIndex}", itemRoot.rectTransform, 18, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                EnsureHistoryTextStyle(label, HistoryTextColor);
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.rectTransform.anchorMin = new Vector2(0f, 1f);
                label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 1f);

                var elapsed = CreateText($"HistoryElapsed{itemIndex}", itemRoot.rectTransform, 12, TextAlignmentOptions.BottomLeft, FontStyles.Normal);
                EnsureHistoryTextStyle(elapsed, HistoryTimeColor);
                elapsed.rectTransform.anchorMin = new Vector2(0f, 0f);
                elapsed.rectTransform.anchorMax = new Vector2(0f, 0f);
                elapsed.rectTransform.pivot = new Vector2(0f, 0f);

                itemRoot.gameObject.SetActive(false);
                _historyItemViews.Add(new HistoryItemView
                {
                    root = itemRoot.rectTransform,
                    label = label,
                    elapsed = elapsed,
                });

                if (itemIndex >= KeyboardChipLimit - 1)
                {
                    continue;
                }

                var separator = CreateText($"HistorySeparator{itemIndex}", panel, 18f, TextAlignmentOptions.Center, FontStyles.Bold);
                EnsureHistoryTextStyle(separator, HistoryTextColor);
                separator.text = "→";
                separator.rectTransform.anchorMin = new Vector2(0f, 0f);
                separator.rectTransform.anchorMax = new Vector2(0f, 0f);
                separator.rectTransform.pivot = new Vector2(0f, 0f);
                separator.gameObject.SetActive(false);
                _historySeparatorViews.Add(separator);
            }
        }

        private void BuildPointerContents(RectTransform layer)
        {
            _pointerRoot = CreateContainer("PointerRoot", layer);
            SetBottomLeftAnchor(_pointerRoot, new Vector2(0.5f, 0.5f));

            _pointerShaft = CreateImage("PointerShaft", _pointerRoot, Texture2D.whiteTexture, PointerIdleColor);
            _pointerShaft.rectTransform.sizeDelta = new Vector2(18f, 3f);
            _pointerShaft.rectTransform.pivot = new Vector2(0f, 0.5f);
            _pointerShaft.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _pointerShaft.rectTransform.localEulerAngles = new Vector3(0f, 0f, -35f);

            _pointerWingTop = CreateImage("PointerWingTop", _pointerRoot, Texture2D.whiteTexture, PointerIdleColor);
            _pointerWingTop.rectTransform.sizeDelta = new Vector2(10f, 3f);
            _pointerWingTop.rectTransform.pivot = new Vector2(0f, 0.5f);
            _pointerWingTop.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _pointerWingTop.rectTransform.localEulerAngles = new Vector3(0f, 0f, 30f);

            _pointerWingBottom = CreateImage("PointerWingBottom", _pointerRoot, Texture2D.whiteTexture, PointerIdleColor);
            _pointerWingBottom.rectTransform.sizeDelta = new Vector2(10f, 3f);
            _pointerWingBottom.rectTransform.pivot = new Vector2(0f, 0.5f);
            _pointerWingBottom.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _pointerWingBottom.rectTransform.localEulerAngles = new Vector3(0f, 0f, -75f);

            _scrollIndicator = CreateText("ScrollIndicator", layer, 22, TextAlignmentOptions.Center, FontStyles.Bold);
            SetBottomLeftAnchor(_scrollIndicator.rectTransform, new Vector2(0.5f, 0.5f));
            _scrollIndicator.gameObject.SetActive(false);
        }

        private TouchView CreateTouchView(int touchId)
        {
            var root = CreateContainer($"Touch{touchId}", _touchLayer);
            SetBottomLeftAnchor(root, new Vector2(0.5f, 0.5f));
            var circle = CreateImage("TouchCircle", root, GetCircleTexture(), new Color(1f, 1f, 1f, 0.28f));
            circle.rectTransform.sizeDelta = new Vector2(TouchDiameter, TouchDiameter);

            var ring = CreateImage("TouchRing", root, GetCircleTexture(), new Color(1f, 1f, 1f, 0.8f));
            ring.rectTransform.sizeDelta = new Vector2(TouchDiameter + 10f, TouchDiameter + 10f);

            var label = CreateText("TouchId", root, 18, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(label.rectTransform, new Vector2(-TouchDiameter * 0.5f, -12f), new Vector2(TouchDiameter * 0.5f, 12f));

            return new TouchView
            {
                root = root,
                label = label,
            };
        }

        private Image EnsureTrailSegment(int index)
        {
            while (_pointerTrailSegments.Count <= index)
            {
                var segment = CreateImage($"TrailSegment{_pointerTrailSegments.Count}", _pointerLayer, Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.5f));
                SetBottomLeftAnchor(segment.rectTransform, new Vector2(0f, 0.5f));
                segment.rectTransform.pivot = new Vector2(0f, 0.5f);
                segment.gameObject.SetActive(false);
                _pointerTrailSegments.Add(segment);
            }

            return _pointerTrailSegments[index];
        }

        private void ConfigureTrailSegment(Image segment, Vector2 start, Vector2 end, float alpha)
        {
            var delta = end - start;
            var distance = delta.magnitude;
            if (distance < 0.01f)
            {
                segment.gameObject.SetActive(false);
                return;
            }

            segment.gameObject.SetActive(true);
            segment.color = new Color(1f, 1f, 1f, alpha * 0.7f);
            segment.rectTransform.anchoredPosition = start;
            segment.rectTransform.sizeDelta = new Vector2(distance, 3f);
            segment.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void AddPointerPulse(Vector2 position, Color color, float now)
        {
            var pulseView = CreateImage($"PointerPulse{_pointerClickPulseViews.Count}", _pointerLayer, GetCircleTexture(), color);
            SetBottomLeftAnchor(pulseView.rectTransform, new Vector2(0.5f, 0.5f));
            pulseView.rectTransform.anchoredPosition = position;
            pulseView.gameObject.SetActive(true);
            _pointerClickPulses.Add(new PointerClickPulse
            {
                position = position,
                startedAt = now,
                color = color,
            });
            _pointerClickPulseViews.Add(pulseView);
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
            _gamepadButtonsByKey[key] = new ButtonVisualState(chip, text, new HeldInputState(label));
        }

        private void CreatePointerButtonChip(HeldInputState state, RectTransform parent, string name, string label, Vector2 position)
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

        private static RectTransform CreateContainer(string name, RectTransform parent)
        {
            var containerObject = new GameObject(name, typeof(RectTransform));
            containerObject.transform.SetParent(parent, false);
            var rectTransform = containerObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return rectTransform;
        }

        private static Image CreatePanel(string name, RectTransform parent, Vector2 size, Color color)
        {
            var image = CreateImage(name, parent, Texture2D.whiteTexture, color);
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private static Image CreateImage(string name, RectTransform parent, Texture2D texture, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.sprite = GetSprite(texture);
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, float fontSize, TextAlignmentOptions alignment, FontStyles fontStyle)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.enableWordWrapping = false;
            return text;
        }

        private static void EnsureHistoryTextStyle(TextMeshProUGUI text, Color color)
        {
            text.font = TMP_Settings.defaultFontAsset;
            text.color = color;
        }

        private static void Stretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static void SetBottomLeftAnchor(RectTransform rectTransform, Vector2 pivot)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = pivot;
        }

        private static void AnchorToCorner(RectTransform rectTransform, OverlayCorner corner, float marginX, float marginY)
        {
            switch (corner)
            {
                case OverlayCorner.TopLeft:
                    rectTransform.anchorMin = new Vector2(0f, 1f);
                    rectTransform.anchorMax = new Vector2(0f, 1f);
                    rectTransform.pivot = new Vector2(0f, 1f);
                    rectTransform.anchoredPosition = new Vector2(marginX, -marginY);
                    break;
                case OverlayCorner.TopRight:
                    rectTransform.anchorMin = new Vector2(1f, 1f);
                    rectTransform.anchorMax = new Vector2(1f, 1f);
                    rectTransform.pivot = new Vector2(1f, 1f);
                    rectTransform.anchoredPosition = new Vector2(-marginX, -marginY);
                    break;
                case OverlayCorner.BottomLeft:
                    rectTransform.anchorMin = new Vector2(0f, 0f);
                    rectTransform.anchorMax = new Vector2(0f, 0f);
                    rectTransform.pivot = new Vector2(0f, 0f);
                    rectTransform.anchoredPosition = new Vector2(marginX, marginY);
                    break;
                default:
                    rectTransform.anchorMin = new Vector2(1f, 0f);
                    rectTransform.anchorMax = new Vector2(1f, 0f);
                    rectTransform.pivot = new Vector2(1f, 0f);
                    rectTransform.anchoredPosition = new Vector2(-marginX, marginY);
                    break;
            }
        }

        private void AnchorHistoryPanel(RectTransform rectTransform, OverlayCorner corner)
        {
            var silhouetteHeight = Mathf.Max(GamepadPanelHeight, KeyboardPanelHeight);
            switch (corner)
            {
                case OverlayCorner.TopLeft:
                    AnchorToCorner(rectTransform, corner, DefaultWidgetMargin, DefaultWidgetMargin + HistoryPanelSpacing);
                    break;
                case OverlayCorner.TopRight:
                    AnchorToCorner(rectTransform, corner, DefaultWidgetMargin, DefaultWidgetMargin + HistoryPanelSpacing);
                    break;
                case OverlayCorner.BottomLeft:
                    AnchorToCorner(rectTransform, corner, DefaultWidgetMargin, DefaultWidgetMargin + silhouetteHeight + HistoryPanelSpacing);
                    break;
                default:
                    AnchorToCorner(rectTransform, corner, DefaultWidgetMargin, DefaultWidgetMargin + silhouetteHeight + HistoryPanelSpacing);
                    break;
            }
        }

        private void HideAllHistoryViews()
        {
            for (var viewIndex = 0; viewIndex < _historyItemViews.Count; viewIndex++)
            {
                _historyItemViews[viewIndex].root.gameObject.SetActive(false);
            }

            for (var separatorIndex = 0; separatorIndex < _historySeparatorViews.Count; separatorIndex++)
            {
                _historySeparatorViews[separatorIndex].gameObject.SetActive(false);
            }
        }

        private static void ApplyHistoryItemLayout(HistoryItemView itemView, float width, float anchoredPositionX)
        {
            itemView.root.anchoredPosition = new Vector2(anchoredPositionX, 0f);
            itemView.root.sizeDelta = new Vector2(width, HistoryItemHeight);
            itemView.label.rectTransform.anchoredPosition = new Vector2(HistoryItemHorizontalPadding, -HistoryItemVerticalPadding);
            // 18pt Bold の行高（約 24px）より低い矩形だと Ellipsis が「1 行も入らない」と判定して全文を消す。行高＋余裕で確保する
            itemView.label.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width - (HistoryItemHorizontalPadding * 2f)), 28f);
            itemView.elapsed.rectTransform.anchoredPosition = new Vector2(HistoryItemHorizontalPadding, HistoryItemVerticalPadding);
            itemView.elapsed.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width - (HistoryItemHorizontalPadding * 2f)), 16f);
        }

        private static Texture2D GetCircleTexture()
        {
            if (s_circleTexture != null)
            {
                return s_circleTexture;
            }

            const int textureSize = 64;
            s_circleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            s_circleTexture.wrapMode = TextureWrapMode.Clamp;
            var center = (textureSize - 1) * 0.5f;
            var radius = center;
            for (var y = 0; y < textureSize; y++)
            {
                for (var x = 0; x < textureSize; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var normalized = Mathf.Clamp01(1f - ((distance - (radius - 2f)) / 2f));
                    var color = new Color(1f, 1f, 1f, normalized);
                    s_circleTexture.SetPixel(x, y, color);
                }
            }

            s_circleTexture.Apply();
            return s_circleTexture;
        }

        private static Sprite GetSprite(Texture2D texture)
        {
            if (texture == Texture2D.whiteTexture)
            {
                if (s_whiteSprite == null)
                {
                    s_whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }

                return s_whiteSprite;
            }

            if (texture == GetCircleTexture())
            {
                if (s_circleSprite == null)
                {
                    s_circleSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }

                return s_circleSprite;
            }

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        private static Color ParseHtmlColor(string htmlColor)
        {
            if (ColorUtility.TryParseHtmlString(htmlColor, out var color))
            {
                return color;
            }

            return Color.white;
        }

        private static void UpdateButtonVisual(TextMeshProUGUI label, Image background, HeldInputState state, Color activeColor, Color fadingColor, float now, float holdSeconds)
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

        private void RefreshMouseButtonVisual(HeldInputState state, float now)
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

        private void UpdateStickState(ref Vector2 liveValue, ref Vector2 ghostValue, ref float ghostReleasedAt, Vector2 newValue, float now)
        {
            var clampedValue = Vector2.ClampMagnitude(newValue, 1f);
            liveValue = clampedValue;
            if (clampedValue.sqrMagnitude > StickCenterThreshold)
            {
                ghostValue = clampedValue;
                ghostReleasedAt = now;
                return;
            }

            if (ghostValue.sqrMagnitude > StickCenterThreshold)
            {
                ghostReleasedAt = now;
            }
        }

        private void UpdatePointerButtonState(HeldInputState state, bool isPressed, float now, Color pulseColor, string historyLabel)
        {
            var wasPressed = state.isPressed;
            UpdateHeldState(state, isPressed, now, historyLabel, true);
            if (isPressed && !wasPressed)
            {
                AddPointerPulse(_pointerRoot.anchoredPosition, pulseColor, now);
            }
        }

        private void UpdateHeldState(HeldInputState state, bool isPressed, float now, string historyLabel, bool addHistoryOnPressed)
        {
            if (state == null)
            {
                return;
            }

            if (isPressed)
            {
                var isNewPress = !state.isPressed;
                if (isNewPress)
                {
                    state.repeatCount = state.IsVisible(now, _options.holdSeconds) ? state.repeatCount + 1 : 1;
                    state.lastPressedAt = now;
                    state.lastReleasedAt = -1f;
                    state.lastVisibleAt = now;
                    if (addHistoryOnPressed && !string.IsNullOrEmpty(historyLabel))
                    {
                        AddHistoryEntry(historyLabel, now);
                    }
                }
                else
                {
                    state.lastVisibleAt = now;
                }

                state.isPressed = true;
                return;
            }

            if (!state.isPressed)
            {
                return;
            }

            state.isPressed = false;
            state.lastReleasedAt = now;
            state.lastVisibleAt = now;
        }

        private void AddHistoryEntry(string label, float now)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            _historyEntries.Add(new HistoryEntry
            {
                label = label,
                startedAt = now,
                elapsedTenths = -1,
                cachedElapsedText = "0.0s",
            });

            var historyCount = Mathf.Max(0, _options.historyCount);
            while (_historyEntries.Count > historyCount)
            {
                _historyEntries.RemoveAt(0);
            }
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
            foreach (var pair in _keyboardStatesByKey)
            {
                if (pair.Value.IsVisible(now, _options.holdSeconds))
                {
                    return true;
                }
            }

            return _leftMouseButtonState.IsVisible(now, _options.holdSeconds)
                || _rightMouseButtonState.IsVisible(now, _options.holdSeconds)
                || _middleMouseButtonState.IsVisible(now, _options.holdSeconds);
        }

        private bool IsAnyPointerButtonActive(float now)
        {
            return _leftMouseButtonState.IsVisible(now, _options.holdSeconds)
                || _rightMouseButtonState.IsVisible(now, _options.holdSeconds)
                || _middleMouseButtonState.IsVisible(now, _options.holdSeconds);
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

        private static HeldInputState CreatePointerButtonState(string label)
        {
            return new HeldInputState(label);
        }

        private enum DeviceMode
        {
            None = 0,
            Gamepad = 1,
            KeyboardMouse = 2,
        }

        private sealed class ButtonVisualState
        {
            public ButtonVisualState(Image background, TextMeshProUGUI text, HeldInputState state)
            {
                this.background = background;
                this.text = text;
                this.state = state;
            }

            public readonly Image background;
            public readonly TextMeshProUGUI text;
            public readonly HeldInputState state;
        }

        private sealed class HeldInputState
        {
            public HeldInputState(string baseLabel)
            {
                this.baseLabel = baseLabel;
                repeatCount = 1;
                lastReleasedAt = -1f;
            }

            public readonly string baseLabel;
            public Image background;
            public TextMeshProUGUI label;
            public bool isPressed;
            public int repeatCount;
            public float lastPressedAt;
            public float lastReleasedAt;
            public float lastVisibleAt;

            public void BindVisual(Image backgroundImage, TextMeshProUGUI labelText)
            {
                background = backgroundImage;
                label = labelText;
            }

            public bool IsVisible(float now, float holdSeconds)
            {
                if (isPressed)
                {
                    return true;
                }

                if (lastReleasedAt < 0f)
                {
                    return false;
                }

                return now - lastReleasedAt < holdSeconds + HoldFadeSeconds;
            }

            public float GetAlpha(float now, float holdSeconds)
            {
                if (isPressed)
                {
                    return 1f;
                }

                if (lastReleasedAt < 0f)
                {
                    return 0f;
                }

                var clampedHoldSeconds = Mathf.Max(0.01f, holdSeconds);
                var elapsed = now - lastReleasedAt;
                if (elapsed <= clampedHoldSeconds)
                {
                    return 1f;
                }

                var fadeElapsed = elapsed - clampedHoldSeconds;
                if (fadeElapsed >= HoldFadeSeconds)
                {
                    return 0f;
                }

                return 1f - (fadeElapsed / HoldFadeSeconds);
            }

            public string GetDisplayText()
            {
                if (repeatCount <= 1)
                {
                    return baseLabel;
                }

                return $"{baseLabel} ×{repeatCount}";
            }
        }

        private sealed class HeldInputStateComparer : IComparer<HeldInputState>
        {
            public static readonly HeldInputStateComparer Instance = new HeldInputStateComparer();

            public int Compare(HeldInputState x, HeldInputState y)
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
            public RectTransform root;
            public Image background;
            public TextMeshProUGUI label;
        }

        private struct PointerTrailPoint
        {
            public Vector2 position;
            public float recordedAt;
        }

        private struct PointerClickPulse
        {
            public Vector2 position;
            public float startedAt;
            public Color color;
        }

        private struct TouchView
        {
            public RectTransform root;
            public TextMeshProUGUI label;
        }

        private struct HistoryEntry
        {
            public string label;
            public float startedAt;
            public int elapsedTenths;
            public string cachedElapsedText;
        }

        private struct HistoryItemView
        {
            public RectTransform root;
            public TextMeshProUGUI label;
            public TextMeshProUGUI elapsed;
        }

        /// <summary>
        /// タッチの必要最小限データです。
        /// 実装差の大きい入力 API から描画側を切り離して簡素化します。
        /// </summary>
        public struct TouchSnapshot
        {
            /// <summary>
            /// 指を識別する ID です。
            /// マルチタッチを継続的に追跡するため保持します。
            /// </summary>
            public int touchId;

            /// <summary>
            /// 画面上の位置です。
            /// 録画上で実際にどこを触ったかを直接示します。
            /// </summary>
            public Vector2 position;
        }

        private sealed class LegacyInputProxy
        {
            private readonly List<string> _pressedKeys = new List<string>();
            private readonly List<TouchSnapshot> _touches = new List<TouchSnapshot>();
            private readonly List<KeyCode> _keyboardKeyCodes = new List<KeyCode>();

            public LegacyInputProxy()
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
                    _touches.Add(new TouchSnapshot
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

        private sealed class InputSystemProxy
        {
            private readonly List<string> _pressedKeys = new List<string>();
            private readonly List<TouchSnapshot> _touches = new List<TouchSnapshot>();
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
                    _touches.Add(new TouchSnapshot
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
}
#endif
