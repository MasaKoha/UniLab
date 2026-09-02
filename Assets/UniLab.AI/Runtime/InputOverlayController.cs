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
    /// 録画後合成ではなくゲーム画面そのものへ載せることで時刻ずれを避けます。
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
        private const float KeyboardChipWidth = 84f;
        private const float KeyboardChipHeight = 34f;
        private const float TouchDiameter = 56f;
        private const float StickRange = 18f;
        private const float DefaultWidgetMargin = 24f;
        private const string WhiteColorHtml = "#FFFFFF";
        private const string YellowColorHtml = "#FFE16A";
        private const string BlueColorHtml = "#79C9FF";

        private static Texture2D s_circleTexture;
        private static Sprite s_whiteSprite;
        private static Sprite s_circleSprite;

        private readonly Dictionary<string, float> _gamepadVisibleUntilByKey = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, TextMeshProUGUI> _gamepadTextByKey = new Dictionary<string, TextMeshProUGUI>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _keyboardVisibleUntilByKey = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<KeyboardChipView> _keyboardChipViews = new List<KeyboardChipView>();
        private readonly List<PointerTrailPoint> _pointerTrailPoints = new List<PointerTrailPoint>();
        private readonly List<Image> _pointerTrailSegments = new List<Image>();
        private readonly List<PointerClickPulse> _pointerClickPulses = new List<PointerClickPulse>();
        private readonly List<Image> _pointerClickPulseViews = new List<Image>();
        private readonly Dictionary<int, TouchView> _touchViewsById = new Dictionary<int, TouchView>();
        private readonly List<int> _releasedTouchIds = new List<int>();
        private readonly LegacyInputProxy _legacyInputProxy = new LegacyInputProxy();
        private readonly InputSystemProxy _inputSystemProxy = new InputSystemProxy();
        private readonly StepLabelProvider _stepLabelProvider = new StepLabelProvider();

        private InputOverlayOptions _options;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _rootTransform;
        private RectTransform _gamepadPanel;
        private RectTransform _keyboardPanel;
        private RectTransform _pointerLayer;
        private RectTransform _touchLayer;
        private RectTransform _topLabelPanel;
        private TextMeshProUGUI _stepLabel;
        private RectTransform _pointerRoot;
        private TextMeshProUGUI _scrollIndicator;
        private Image _leftStickDot;
        private Image _rightStickDot;
        private bool _isInitialized;
        private bool _wasLeftPointerPressed;
        private bool _wasRightPointerPressed;
        private bool _wasMiddlePointerPressed;
        private Vector2 _previousPointerPosition;
        private Vector2 _leftStickValue;
        private Vector2 _rightStickValue;
        private float _scrollIndicatorVisibleUntil;

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

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            PollInput(now);
            RefreshGamepad(now);
            RefreshKeyboard(now);
            RefreshPointer(now);
            RefreshTouches(now);
            RefreshStepLabel(now);
        }

        private void OnDestroy()
        {
            _keyboardChipViews.Clear();
            _pointerTrailSegments.Clear();
            _pointerClickPulseViews.Clear();
            _touchViewsById.Clear();
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

            var gamepadPanelObject = CreatePanel("GamepadPanel", _rootTransform, new Vector2(340f, 190f), new Color(0f, 0f, 0f, 0.45f));
            _gamepadPanel = gamepadPanelObject.rectTransform;
            BuildGamepadContents(_gamepadPanel);

            var keyboardPanelObject = CreatePanel("KeyboardPanel", _rootTransform, new Vector2(720f, 50f), new Color(0f, 0f, 0f, 0.35f));
            _keyboardPanel = keyboardPanelObject.rectTransform;
            BuildKeyboardContents(_keyboardPanel);

            _pointerLayer = CreateContainer("PointerLayer", _rootTransform);
            BuildPointerContents(_pointerLayer);

            _touchLayer = CreateContainer("TouchLayer", _rootTransform);

            var topLabelPanelObject = CreatePanel("TopLabelPanel", _rootTransform, new Vector2(860f, 46f), new Color(0f, 0f, 0f, 0.35f));
            _topLabelPanel = topLabelPanelObject.rectTransform;
            _stepLabel = CreateText("StepLabel", _topLabelPanel, 22, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(_stepLabel.rectTransform, new Vector2(10f, 6f), new Vector2(-10f, -6f));
        }

        private void ApplyOptions()
        {
            _canvasGroup.alpha = Mathf.Clamp01(_options.opacity);

            var scale = Mathf.Max(0.1f, _options.scale);
            _gamepadPanel.localScale = Vector3.one * scale;
            _keyboardPanel.localScale = Vector3.one * scale;
            _topLabelPanel.localScale = Vector3.one * scale;

            _gamepadPanel.gameObject.SetActive(_options.showGamepad);
            _keyboardPanel.gameObject.SetActive(_options.showKeyboard);
            _pointerLayer.gameObject.SetActive(_options.showPointer);
            _touchLayer.gameObject.SetActive(_options.showTouch);
            _topLabelPanel.gameObject.SetActive(_options.showStepLabel);

            AnchorToCorner(_gamepadPanel, _options.gamepadCorner, DefaultWidgetMargin, DefaultWidgetMargin);
            AnchorToCorner(_keyboardPanel, OverlayCorner.BottomLeft, DefaultWidgetMargin, DefaultWidgetMargin);
            AnchorToCorner(_topLabelPanel, OverlayCorner.TopLeft, DefaultWidgetMargin, DefaultWidgetMargin);
            _topLabelPanel.anchorMax = new Vector2(1f, 1f);
            _topLabelPanel.pivot = new Vector2(0.5f, 1f);
            _topLabelPanel.anchoredPosition = new Vector2(0f, -DefaultWidgetMargin);
        }

        private void PollInput(float now)
        {
            if (_inputSystemProxy.TryPoll(this, now))
            {
                return;
            }

            _legacyInputProxy.Poll(this, now);
        }

        private void RefreshGamepad(float now)
        {
            if (!_options.showGamepad)
            {
                return;
            }

            var hasAnyVisibleButton = false;
            foreach (var pair in _gamepadTextByKey)
            {
                var isVisible = _gamepadVisibleUntilByKey.TryGetValue(pair.Key, out var visibleUntil) && visibleUntil >= now;
                hasAnyVisibleButton |= isVisible;
                SetChipActive(pair.Value, isVisible, isVisible ? new Color(0.15f, 0.85f, 0.45f, 0.95f) : new Color(1f, 1f, 1f, 0.25f));
            }

            _leftStickDot.rectTransform.anchoredPosition = _leftStickValue * StickRange;
            _rightStickDot.rectTransform.anchoredPosition = _rightStickValue * StickRange;
            _gamepadPanel.gameObject.SetActive(_options.showGamepad && (hasAnyVisibleButton || _leftStickValue.sqrMagnitude > 0.0001f || _rightStickValue.sqrMagnitude > 0.0001f));
        }

        private void RefreshKeyboard(float now)
        {
            if (!_options.showKeyboard)
            {
                return;
            }

            var visibleKeys = new List<string>();
            foreach (var pair in _keyboardVisibleUntilByKey)
            {
                if (pair.Value >= now)
                {
                    visibleKeys.Add(pair.Key);
                }
            }

            visibleKeys.Sort(StringComparer.Ordinal);
            for (var chipIndex = 0; chipIndex < _keyboardChipViews.Count; chipIndex++)
            {
                var chipView = _keyboardChipViews[chipIndex];
                var isActive = chipIndex < visibleKeys.Count;
                chipView.root.gameObject.SetActive(isActive);
                if (!isActive)
                {
                    continue;
                }

                chipView.label.text = visibleKeys[chipIndex];
            }

            _keyboardPanel.gameObject.SetActive(_options.showKeyboard && visibleKeys.Count > 0);
        }

        private void RefreshPointer(float now)
        {
            if (!_options.showPointer)
            {
                return;
            }

            _pointerRoot.gameObject.SetActive(true);

            for (var pulseIndex = _pointerClickPulses.Count - 1; pulseIndex >= 0; pulseIndex--)
            {
                var pulse = _pointerClickPulses[pulseIndex];
                var elapsed = now - pulse.startedAt;
                if (elapsed > PointerRingDurationSeconds)
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

        private void RefreshStepLabel(float now)
        {
            if (!_options.showStepLabel)
            {
                return;
            }

            var labelText = _stepLabelProvider.TryGetCurrentLabel(now);
            var hasLabel = !string.IsNullOrEmpty(labelText);
            _topLabelPanel.gameObject.SetActive(hasLabel);
            if (hasLabel)
            {
                _stepLabel.text = labelText;
            }
        }

        /// <summary>
        /// パッドボタン押下を一定時間可視にします。
        /// 1 フレーム押下でも録画中に読める長さを保証するためです。
        /// </summary>
        public void MarkGamepadButtonPressed(string buttonKey, float now)
        {
            if (string.IsNullOrEmpty(buttonKey))
            {
                return;
            }

            _gamepadVisibleUntilByKey[buttonKey] = now + Mathf.Max(0.01f, _options.minimumVisibleSeconds);
        }

        /// <summary>
        /// パッドスティック位置を更新します。
        /// ボタンだけでは移動入力の方向を判断できないため座標も保持します。
        /// </summary>
        public void SetGamepadSticks(Vector2 leftStick, Vector2 rightStick)
        {
            _leftStickValue = Vector2.ClampMagnitude(leftStick, 1f);
            _rightStickValue = Vector2.ClampMagnitude(rightStick, 1f);
        }

        /// <summary>
        /// キー押下を一定時間可視にします。
        /// 録画上で見落としやすい短い入力も読めるようにするためです。
        /// </summary>
        public void MarkKeyboardKeyPressed(string keyName, float now)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return;
            }

            _keyboardVisibleUntilByKey[keyName] = now + Mathf.Max(0.01f, _options.minimumVisibleSeconds);
        }

        /// <summary>
        /// 現在押されているキーボード集合を更新します。
        /// 押しっぱなし維持と押下開始検出の両方を polling だけで再現するためです。
        /// </summary>
        public void ReplacePressedKeyboardKeys(List<string> pressedKeys, float now)
        {
            for (var keyIndex = 0; keyIndex < pressedKeys.Count; keyIndex++)
            {
                var keyName = pressedKeys[keyIndex];
                _keyboardVisibleUntilByKey[keyName] = now + Mathf.Max(0.01f, _options.minimumVisibleSeconds);
            }
        }

        /// <summary>
        /// ポインタ位置を更新します。
        /// OS カーソルが録画へ写らない環境でも操作点を失わないためです。
        /// </summary>
        public void SetPointerPosition(Vector2 screenPosition)
        {
            _pointerRoot.anchoredPosition = screenPosition;
            _scrollIndicator.rectTransform.anchoredPosition = screenPosition + new Vector2(18f, 24f);
        }

        /// <summary>
        /// ポインタボタン状態を更新します。
        /// クリック波紋とドラッグ軌跡を区別して残すため前フレーム状態もここで管理します。
        /// </summary>
        public void SetPointerButtons(bool isLeftPressed, bool isRightPressed, bool isMiddlePressed, float now)
        {
            if (isLeftPressed && !_wasLeftPointerPressed)
            {
                AddPointerPulse(_pointerRoot.anchoredPosition, ParseHtmlColor(WhiteColorHtml), now);
            }

            if (isRightPressed && !_wasRightPointerPressed)
            {
                AddPointerPulse(_pointerRoot.anchoredPosition, ParseHtmlColor(YellowColorHtml), now);
            }

            if (isMiddlePressed && !_wasMiddlePointerPressed)
            {
                AddPointerPulse(_pointerRoot.anchoredPosition, ParseHtmlColor(BlueColorHtml), now);
            }

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

            _wasLeftPointerPressed = isLeftPressed;
            _wasRightPointerPressed = isRightPressed;
            _wasMiddlePointerPressed = isMiddlePressed;
            _previousPointerPosition = _pointerRoot.anchoredPosition;
        }

        /// <summary>
        /// スクロール表示を短時間だけ残します。
        /// 動画上でホイール操作を座標変化なしに読めるようにするためです。
        /// </summary>
        public void ShowScroll(Vector2 delta, float now)
        {
            if (Mathf.Abs(delta.y) < 0.01f)
            {
                return;
            }

            _scrollIndicator.text = delta.y > 0f ? "^" : "v";
            _scrollIndicatorVisibleUntil = now + ScrollIndicatorDurationSeconds;
        }

        /// <summary>
        /// アクティブなタッチ一覧を反映します。
        /// マルチタッチを個別に可視化するため指 ID ごとに UI を分けて管理します。
        /// </summary>
        public void ReplaceTouches(List<TouchSnapshot> touches)
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
                }

                touchView.root.anchoredPosition = touch.position;
                touchView.label.text = touch.touchId.ToString();
                touchView.root.gameObject.SetActive(true);
                _releasedTouchIds.Remove(touch.touchId);
            }
        }

        private void BuildGamepadContents(RectTransform panel)
        {
            CreateChip("LB", panel, new Vector2(54f, -28f), new Vector2(52f, 28f));
            CreateChip("RB", panel, new Vector2(286f, -28f), new Vector2(52f, 28f));
            CreateChip("Up", panel, new Vector2(60f, -68f), new Vector2(36f, 36f), "^");
            CreateChip("Left", panel, new Vector2(34f, -94f), new Vector2(36f, 36f), "<");
            CreateChip("Right", panel, new Vector2(86f, -94f), new Vector2(36f, 36f), ">");
            CreateChip("Down", panel, new Vector2(60f, -120f), new Vector2(36f, 36f), "v");
            CreateChip("X", panel, new Vector2(236f, -84f), new Vector2(40f, 40f));
            CreateChip("Y", panel, new Vector2(262f, -58f), new Vector2(40f, 40f));
            CreateChip("A", panel, new Vector2(262f, -110f), new Vector2(40f, 40f));
            CreateChip("B", panel, new Vector2(288f, -84f), new Vector2(40f, 40f));
            CreateChip("Select", panel, new Vector2(146f, -118f), new Vector2(68f, 28f), "SEL");
            CreateChip("Start", panel, new Vector2(218f, -118f), new Vector2(68f, 28f), "START");

            CreateStickDisplay("LS", panel, new Vector2(100f, -150f), out _leftStickDot);
            CreateStickDisplay("RS", panel, new Vector2(240f, -150f), out _rightStickDot);
        }

        private void BuildKeyboardContents(RectTransform panel)
        {
            for (var chipIndex = 0; chipIndex < KeyboardChipLimit; chipIndex++)
            {
                var chipObject = CreatePanel($"KeyChip{chipIndex}", panel, new Vector2(KeyboardChipWidth, KeyboardChipHeight), new Color(0f, 0f, 0f, 0.55f));
                chipObject.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                chipObject.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                chipObject.rectTransform.pivot = new Vector2(0f, 0.5f);
                chipObject.rectTransform.anchoredPosition = new Vector2(12f + (chipIndex * (KeyboardChipWidth + 8f)), 0f);
                var label = CreateText($"KeyChipLabel{chipIndex}", chipObject.rectTransform, 20, TextAlignmentOptions.Center, FontStyles.Bold);
                Stretch(label.rectTransform, new Vector2(6f, 4f), new Vector2(-6f, -4f));
                chipObject.gameObject.SetActive(false);

                _keyboardChipViews.Add(new KeyboardChipView
                {
                    root = chipObject.rectTransform,
                    label = label,
                });
            }
        }

        private void BuildPointerContents(RectTransform layer)
        {
            _pointerRoot = CreateContainer("PointerRoot", layer);
            SetBottomLeftAnchor(_pointerRoot, new Vector2(0.5f, 0.5f));

            var shaft = CreateImage("PointerShaft", _pointerRoot, Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.95f));
            shaft.rectTransform.sizeDelta = new Vector2(18f, 3f);
            shaft.rectTransform.pivot = new Vector2(0f, 0.5f);
            shaft.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            shaft.rectTransform.localEulerAngles = new Vector3(0f, 0f, -35f);

            var wingTop = CreateImage("PointerWingTop", _pointerRoot, Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.95f));
            wingTop.rectTransform.sizeDelta = new Vector2(10f, 3f);
            wingTop.rectTransform.pivot = new Vector2(0f, 0.5f);
            wingTop.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            wingTop.rectTransform.localEulerAngles = new Vector3(0f, 0f, 30f);

            var wingBottom = CreateImage("PointerWingBottom", _pointerRoot, Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.95f));
            wingBottom.rectTransform.sizeDelta = new Vector2(10f, 3f);
            wingBottom.rectTransform.pivot = new Vector2(0f, 0.5f);
            wingBottom.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            wingBottom.rectTransform.localEulerAngles = new Vector3(0f, 0f, -75f);

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

        private void CreateStickDisplay(string label, RectTransform parent, Vector2 position, out Image dot)
        {
            var stickRoot = CreateContainer($"{label}Root", parent);
            stickRoot.anchorMin = new Vector2(0f, 1f);
            stickRoot.anchorMax = new Vector2(0f, 1f);
            stickRoot.pivot = new Vector2(0.5f, 0.5f);
            stickRoot.anchoredPosition = position;

            var ring = CreateImage($"{label}Ring", stickRoot, GetCircleTexture(), new Color(1f, 1f, 1f, 0.2f));
            ring.rectTransform.sizeDelta = new Vector2(50f, 50f);

            dot = CreateImage($"{label}Dot", stickRoot, GetCircleTexture(), new Color(0.2f, 0.95f, 0.6f, 0.95f));
            dot.rectTransform.sizeDelta = new Vector2(12f, 12f);

            var text = CreateText($"{label}Text", stickRoot, 16, TextAlignmentOptions.Center, FontStyles.Bold);
            text.text = label;
            text.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            text.rectTransform.sizeDelta = new Vector2(60f, 20f);
        }

        private TextMeshProUGUI CreateChip(string key, RectTransform parent, Vector2 position, Vector2 size)
        {
            return CreateChip(key, parent, position, size, key);
        }

        private TextMeshProUGUI CreateChip(string key, RectTransform parent, Vector2 position, Vector2 size, string label)
        {
            var chip = CreatePanel($"{key}Chip", parent, size, new Color(1f, 1f, 1f, 0.18f));
            chip.rectTransform.anchorMin = new Vector2(0f, 1f);
            chip.rectTransform.anchorMax = new Vector2(0f, 1f);
            chip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            chip.rectTransform.anchoredPosition = position;
            var text = CreateText($"{key}Label", chip.rectTransform, 18, TextAlignmentOptions.Center, FontStyles.Bold);
            text.text = label;
            Stretch(text.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            _gamepadTextByKey[key] = text;
            return text;
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

        private static void SetChipActive(TextMeshProUGUI label, bool isActive, Color backgroundColor)
        {
            if (label == null)
            {
                return;
            }

            var background = label.GetComponentInParent<Image>();
            if (background != null)
            {
                background.color = backgroundColor;
            }

            label.color = isActive ? Color.white : new Color(1f, 1f, 1f, 0.7f);
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

        private struct KeyboardChipView
        {
            public RectTransform root;
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
                controller.SetPointerPosition(Input.mousePosition);
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

                controller.ReplaceTouches(_touches);

                if (Input.GetKey(KeyCode.JoystickButton0))
                {
                    controller.MarkGamepadButtonPressed("A", now);
                }

                if (Input.GetKey(KeyCode.JoystickButton1))
                {
                    controller.MarkGamepadButtonPressed("B", now);
                }

                if (Input.GetKey(KeyCode.JoystickButton2))
                {
                    controller.MarkGamepadButtonPressed("X", now);
                }

                if (Input.GetKey(KeyCode.JoystickButton3))
                {
                    controller.MarkGamepadButtonPressed("Y", now);
                }
            }

            private static string NormalizeLegacyKeyName(KeyCode keyCode)
            {
                var keyName = keyCode.ToString();
                switch (keyCode)
                {
                    case KeyCode.Return:
                        return "Enter";
                    case KeyCode.Escape:
                        return "Esc";
                    case KeyCode.Space:
                        return "Space";
                    default:
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
                PollTouches(controller);
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

                controller.SetPointerPosition(ReadVector2Control(mouse, "position"));
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
                    controller.SetGamepadSticks(Vector2.zero, Vector2.zero);
                    return;
                }

                MarkButtonIfPressed(controller, gamepad, "buttonSouth", "A", now);
                MarkButtonIfPressed(controller, gamepad, "buttonEast", "B", now);
                MarkButtonIfPressed(controller, gamepad, "buttonWest", "X", now);
                MarkButtonIfPressed(controller, gamepad, "buttonNorth", "Y", now);
                MarkButtonIfPressed(controller, gamepad, "leftShoulder", "LB", now);
                MarkButtonIfPressed(controller, gamepad, "rightShoulder", "RB", now);
                MarkButtonIfPressed(controller, gamepad, "startButton", "Start", now);
                MarkButtonIfPressed(controller, gamepad, "selectButton", "Select", now);
                MarkButtonIfPressed(controller, GetMemberValue(gamepad, "dpad"), "up", "Up", now);
                MarkButtonIfPressed(controller, GetMemberValue(gamepad, "dpad"), "down", "Down", now);
                MarkButtonIfPressed(controller, GetMemberValue(gamepad, "dpad"), "left", "Left", now);
                MarkButtonIfPressed(controller, GetMemberValue(gamepad, "dpad"), "right", "Right", now);

                controller.SetGamepadSticks(
                    ReadVector2Control(gamepad, "leftStick"),
                    ReadVector2Control(gamepad, "rightStick"));
            }

            private void PollTouches(InputOverlayController controller)
            {
                _touches.Clear();
                var touchscreen = GetCurrentDevice(_touchscreenType);
                if (touchscreen == null)
                {
                    controller.ReplaceTouches(_touches);
                    return;
                }

                var touches = GetMemberValue(touchscreen, "touches");
                if (touches == null)
                {
                    controller.ReplaceTouches(_touches);
                    return;
                }

                var countProperty = touches.GetType().GetProperty("Count");
                var itemProperty = touches.GetType().GetProperty("Item");
                if (countProperty == null || itemProperty == null)
                {
                    controller.ReplaceTouches(_touches);
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

                controller.ReplaceTouches(_touches);
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

            private static void MarkButtonIfPressed(InputOverlayController controller, object deviceOrControl, string memberName, string overlayKey, float now)
            {
                if (deviceOrControl == null)
                {
                    return;
                }

                if (!ReadButtonControl(deviceOrControl, memberName))
                {
                    return;
                }

                controller.MarkGamepadButtonPressed(overlayKey, now);
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

        private sealed class StepLabelProvider
        {
            private FieldInfo _currentStepField;
            private FieldInfo _stepStartRealtimeField;
            private MethodInfo _createStepMarkerLabelMethod;
            private UiScenarioRunner _cachedRunner;

            /// <summary>
            /// UiScenarioRunner を直接変更せず現在ステップ文字列を取り出します。
            /// このバッチの編集境界を守りつつ録画ラベル要件を満たすためです。
            /// </summary>
            public string TryGetCurrentLabel(float now)
            {
                var runner = GetRunner();
                if (runner == null)
                {
                    return string.Empty;
                }

                EnsureMembers();
                if (_currentStepField == null || _stepStartRealtimeField == null || _createStepMarkerLabelMethod == null)
                {
                    return string.Empty;
                }

                var currentStep = _currentStepField.GetValue(runner);
                if (currentStep == null)
                {
                    return string.Empty;
                }

                var stepStartRealtime = Convert.ToDouble(_stepStartRealtimeField.GetValue(runner));
                var waitedSeconds = now - (float)stepStartRealtime;
                var label = _createStepMarkerLabelMethod.Invoke(runner, new[] { currentStep, (object)(double)Mathf.Max(0f, waitedSeconds) }) as string;
                return label ?? string.Empty;
            }

            private UiScenarioRunner GetRunner()
            {
                if (_cachedRunner != null)
                {
                    return _cachedRunner;
                }

                var runners = UnityEngine.Object.FindObjectsByType<UiScenarioRunner>(FindObjectsSortMode.None);
                _cachedRunner = runners.Length > 0 ? runners[0] : null;
                return _cachedRunner;
            }

            private void EnsureMembers()
            {
                if (_currentStepField != null)
                {
                    return;
                }

                var runnerType = typeof(UiScenarioRunner);
                _currentStepField = runnerType.GetField("_currentStep", BindingFlags.NonPublic | BindingFlags.Instance);
                _stepStartRealtimeField = runnerType.GetField("_stepStartRealtime", BindingFlags.NonPublic | BindingFlags.Instance);
                _createStepMarkerLabelMethod = runnerType.GetMethod("CreateStepMarkerLabel", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }
    }
}
#endif
