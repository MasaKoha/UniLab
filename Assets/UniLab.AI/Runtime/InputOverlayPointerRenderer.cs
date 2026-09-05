#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UniLab.AI.InputOverlayVisualPrimitives;

namespace UniLab.AI
{
    /// <summary>ポインタ・クリック波紋・軌跡・タッチの描画を保持します。</summary>
    internal sealed class InputOverlayPointerRenderer
    {
        private readonly InputOverlayInputState _inputState;
        private readonly InputOverlayHistory _history;
        private readonly Action<float> _registerKeyboardActivity;
        private readonly Func<bool> _isKeyboardMouseDisplayed;
        private InputOverlayOptions _options;

        /// <summary>通知先を固定し、毎フレームのアロケーションを増やさず機器表示と連携します。</summary>
        internal InputOverlayPointerRenderer(InputOverlayInputState inputState, InputOverlayHistory history,
            Action<float> registerKeyboardActivity, Func<bool> isKeyboardMouseDisplayed)
        {
            _inputState = inputState;
            _history = history;
            _registerKeyboardActivity = registerKeyboardActivity;
            _isKeyboardMouseDisplayed = isKeyboardMouseDisplayed;
            _leftMouseButtonState = CreatePointerButtonState("L");
            _rightMouseButtonState = CreatePointerButtonState("R");
            _middleMouseButtonState = CreatePointerButtonState("M");
        }

        /// <summary>左ボタンの表示をキーボード模式図と共有します。</summary>
        internal InputOverlayHeldState LeftMouseButtonState => _leftMouseButtonState;
        /// <summary>右ボタンの表示をキーボード模式図と共有します。</summary>
        internal InputOverlayHeldState RightMouseButtonState => _rightMouseButtonState;
        /// <summary>中央ボタンの表示をキーボード模式図と共有します。</summary>
        internal InputOverlayHeldState MiddleMouseButtonState => _middleMouseButtonState;
        /// <summary>座標ラベルとカーソルで同じ入力位置を使います。</summary>
        internal Vector2 PointerPosition => _pointerPosition;

        /// <summary>履歴帯より後の描画順を維持してポインタの構造を作ります。</summary>
        internal void BuildVisualTree(RectTransform rootTransform)
        {
            _pointerLayer = CreateContainer("PointerLayer", rootTransform);
            BuildPointerContents(_pointerLayer);
            _touchLayer = CreateContainer("TouchLayer", rootTransform);
        }

        /// <summary>表示設定を他の描画と揃えます。</summary>
        internal void ApplyOptions(InputOverlayOptions options)
        {
            _options = options;
        }

        /// <summary>破棄時に一時描画の参照を解放します。</summary>
        internal void Clear()
        {
            _pointerTrailSegments.Clear();
            _pointerClickPulseViews.Clear();
            _touchViewsById.Clear();
        }

        private const int DragTrailSegmentLimit = 24;
        private const float PointerRingDurationSeconds = 0.35f;
        private const float DragTrailFadeSeconds = 0.5f;
        private const float ScrollIndicatorDurationSeconds = 0.2f;
        private const float PointerMoveThreshold = 2f;
        private const float TouchDiameter = 56f;
        private static readonly Color PointerIdleColor = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color PointerActiveColor = new Color(1f, 1f, 1f, 0.95f);
        private const string WhiteColorHtml = "#FFFFFF";
        private const string YellowColorHtml = "#FFE16A";
        private const string BlueColorHtml = "#79C9FF";
        private readonly List<PointerTrailPoint> _pointerTrailPoints = new List<PointerTrailPoint>();
        private readonly List<Image> _pointerTrailSegments = new List<Image>();
        private readonly List<PointerClickPulse> _pointerClickPulses = new List<PointerClickPulse>();
        private readonly List<Image> _pointerClickPulseViews = new List<Image>();
        private readonly Dictionary<int, TouchView> _touchViewsById = new Dictionary<int, TouchView>();
        private readonly List<int> _releasedTouchIds = new List<int>();
        private RectTransform _pointerLayer;
        private RectTransform _touchLayer;
        private RectTransform _pointerRoot;
        private TextMeshProUGUI _scrollIndicator;
        private Image _pointerShaft;
        private Image _pointerWingTop;
        private Image _pointerWingBottom;
        private InputOverlayHeldState _leftMouseButtonState;
        private InputOverlayHeldState _rightMouseButtonState;
        private InputOverlayHeldState _middleMouseButtonState;
        private Vector2 _previousPointerPosition;
        private Vector2 _pointerPosition;
        private float _scrollIndicatorVisibleUntil;

        /// <summary>ポインタとタッチの表示寿命を既存の規則で更新します。</summary>
        internal void RefreshPointer(float now)
        {
            var shouldShow = _options.showPointer && _isKeyboardMouseDisplayed();
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

        /// <summary>ポインタとタッチの表示寿命を既存の規則で更新します。</summary>
        internal void RefreshTouches(float now)
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

                UnityEngine.Object.Destroy(touchView.root.gameObject);
                _touchViewsById.Remove(releasedTouchId);
            }

            _releasedTouchIds.Clear();
        }

        /// <summary>
        /// ポインタ位置を更新します。
        /// キーボード＋マウス模式図の利用機器判定と画面上カーソル描画の両方で使うためです。
        /// </summary>
        public void SetPointerPosition(Vector2 screenPosition, float now)
        {
            if (Vector2.Distance(_pointerPosition, screenPosition) >= PointerMoveThreshold)
            {
                _registerKeyboardActivity(now);
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
                _registerKeyboardActivity(now);
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

            _registerKeyboardActivity(now);
            _scrollIndicator.text = delta.y > 0f ? "^" : "v";
            _scrollIndicatorVisibleUntil = now + ScrollIndicatorDurationSeconds;
        }

        /// <summary>
        /// アクティブなタッチ一覧を反映します。
        /// タップ開始だけ履歴へ残しつつ、描画そのものは接触中の指に限定します。
        /// </summary>
        public void ReplaceTouches(List<InputOverlayController.TouchSnapshot> touches, float now)
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
                    _history.AddHistoryEntry("Tap", now);
                }

                touchView.root.anchoredPosition = touch.position;
                touchView.label.text = touch.touchId.ToString();
                touchView.root.gameObject.SetActive(true);
                _releasedTouchIds.Remove(touch.touchId);
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

        private void UpdatePointerButtonState(InputOverlayHeldState state, bool isPressed, float now, Color pulseColor, string historyLabel)
        {
            var wasPressed = state.isPressed;
            _inputState.UpdateHeldState(state, isPressed, now, historyLabel, true);
            if (isPressed && !wasPressed)
            {
                AddPointerPulse(_pointerRoot.anchoredPosition, pulseColor, now);
            }
        }

        /// <summary>ポインタとタッチの表示寿命を既存の規則で更新します。</summary>
        internal bool IsAnyPointerButtonActive(float now)
        {
            return _leftMouseButtonState.IsVisible(now, _options.holdSeconds)
                || _rightMouseButtonState.IsVisible(now, _options.holdSeconds)
                || _middleMouseButtonState.IsVisible(now, _options.holdSeconds);
        }

        private static InputOverlayHeldState CreatePointerButtonState(string label)
        {
            return new InputOverlayHeldState(label);
        }

        private struct PointerTrailPoint
        {
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public Vector2 position;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public float recordedAt;
        }

        private struct PointerClickPulse
        {
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public Vector2 position;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public float startedAt;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public Color color;
        }

        private struct TouchView
        {
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public RectTransform root;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public TextMeshProUGUI label;
        }
    }
}
#endif
