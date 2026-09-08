using System;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI
{
    /// <summary>
    /// 離散セグメントと部分充填を 1 枚のメッシュで描画するバー。
    /// 呼び出し側がラベルを別配置できるよう、各セグメント矩形も返す。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SegmentedBarView : MaskableGraphic
    {
        private const int MinimumSegmentCount = 1;
        private const int DefaultSegmentCount = 1;
        private const float SegmentVisibilityThresholdPixels = 1f;
        private const float FullProgress = 1f;
        private const float EmptyProgress = 0f;
        private const float FullAlphaThreshold = 0f;
        private readonly UIVertex[] _quadVertices = new UIVertex[4];
        private readonly UIVertex[] _outlineQuadVertices = new UIVertex[4];
        private Rect[] _segmentRects = Array.Empty<Rect>();
        private float _normalizedValue;
        private int _segmentCount;
        private bool _isInitialized;
        private SegmentedBarStyle _style = SegmentedBarStyle.Default;
        private float _glowIntensity;
        private SegmentedBarAnimation _animation;
        /// <summary>
        /// 値アニメーション、溜め演出、弾け演出のいずれかが再生中かを返す。
        /// </summary>
        public bool IsAnimating { get; private set; }
        /// <summary>
        /// セグメント数を確定し、矩形キャッシュを確保する。
        /// 1 未満は 1 に丸め、呼び出し順による例外を避ける。
        /// </summary>
        public void Initialize(int segmentCount)
        {
            EnsureAnimation();
            _animation.StopAllAnimations(restorePosition: true, resetGlow: false);
            _segmentCount = Mathf.Max(MinimumSegmentCount, segmentCount);
            _segmentRects = new Rect[_segmentCount];
            _normalizedValue = Mathf.Clamp01(_normalizedValue);
            _isInitialized = true;
            UpdateSegmentRects();
            SetVerticesDirty();
        }
        /// <summary>
        /// 正規化済み値を更新し、最後の 1 セグメントだけ部分充填できる形で保持する。
        /// </summary>
        public void SetValue(float normalizedValue)
        {
            EnsureInitialized();
            EnsureAnimation();
            _animation.StopAllAnimations(restorePosition: true, resetGlow: false);
            _normalizedValue = Mathf.Clamp01(normalizedValue);
            SetVerticesDirty();
        }
        /// <summary>
        /// 描画スタイルを差し替える。
        /// </summary>
        public void SetStyle(SegmentedBarStyle style)
        {
            _style = style;
            if (_isInitialized)
            {
                UpdateSegmentRects();
            }

            SetVerticesDirty();
        }
        /// <summary>
        /// 現在値から指定値まで補間し、毎フレーム再描画する。
        /// </summary>
        public void AnimateTo(float normalizedValue, float durationSeconds, RadarChartEasing easing)
        {
            EnsureInitialized();
            EnsureAnimation();
            _animation.AnimateTo(normalizedValue, durationSeconds, easing);
        }
        /// <summary>
        /// 塗り色を発光代用の色へ 0〜1 で補間する。
        /// </summary>
        public void SetGlow(float intensity)
        {
            _glowIntensity = Mathf.Clamp01(intensity);
            SetVerticesDirty();
        }
        /// <summary>
        /// 発光を溜めつつ、バー全体を微振動させる。
        /// </summary>
        public void PlayCharge(float durationSeconds, float shakeAmplitudePixels)
        {
            EnsureInitialized();
            EnsureAnimation();
            _animation.PlayCharge(durationSeconds, shakeAmplitudePixels);
        }
        /// <summary>
        /// 発光を減衰させながら値を 0 に戻す。
        /// </summary>
        public void PlayBurst(float durationSeconds)
        {
            EnsureInitialized();
            EnsureAnimation();
            _animation.PlayBurst(durationSeconds);
        }
        /// <summary>
        /// 指定セグメントの局所矩形を返す。ラベル配置の基準を呼び出し側に委ねるために使う。
        /// </summary>
        public Rect GetSegmentLocalRect(int index)
        {
            EnsureInitialized();
            ValidateSegmentIndex(index);
            UpdateSegmentRects();
            return _segmentRects[index];
        }
        /// <summary>
        /// 指定セグメントの現在の塗り矩形を返す。部分充填率を外部で検証したいときに使う。
        /// </summary>
        public Rect GetFilledSegmentLocalRect(int index)
        {
            EnsureInitialized();
            ValidateSegmentIndex(index);
            UpdateSegmentRects();
            return GetFilledRect(_segmentRects[index], GetSegmentFillAmount(index));
        }

        /// <summary>
        /// 内部状態から UI メッシュを再構築する。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!_isInitialized)
            {
                return;
            }

            UpdateSegmentRects();
            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            AddSegmentBackgrounds(vertexHelper);
            AddFilledSegments(vertexHelper);
            AddSeparators(vertexHelper);
            AddOutline(vertexHelper, rect, _style.OutlineColor, _style.OutlineThickness);
        }
        protected override void OnDestroy()
        {
            _animation?.Dispose();
            _animation = null;
            base.OnDestroy();
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            Initialize(DefaultSegmentCount);
        }
        private void ValidateSegmentIndex(int index)
        {
            if (index < 0 || index >= _segmentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "セグメントインデックスが範囲外。");
            }
        }
        private void EnsureAnimation()
        {
            if (_animation == null)
            {
                _animation = new SegmentedBarAnimation(this);
            }
        }
        private void UpdateSegmentRects()
        {
            if (!_isInitialized)
            {
                return;
            }

            var rect = rectTransform.rect;
            var totalSpacing = _style.SegmentSpacing * (_segmentCount - 1);
            if (_style.Vertical)
            {
                var segmentHeight = Mathf.Max(0f, (rect.height - totalSpacing) / _segmentCount);
                var y = rect.yMin;
                for (var segmentIndex = 0; segmentIndex < _segmentCount; segmentIndex++)
                {
                    _segmentRects[segmentIndex] = new Rect(rect.xMin, y, rect.width, segmentHeight);
                    y += segmentHeight + _style.SegmentSpacing;
                }
                return;
            }
            var segmentWidth = Mathf.Max(0f, (rect.width - totalSpacing) / _segmentCount);
            var x = rect.xMin;
            for (var segmentIndex = 0; segmentIndex < _segmentCount; segmentIndex++)
            {
                _segmentRects[segmentIndex] = new Rect(x, rect.yMin, segmentWidth, rect.height);
                x += segmentWidth + _style.SegmentSpacing;
            }
        }
        private void AddSegmentBackgrounds(VertexHelper vertexHelper)
        {
            if (_style.BackgroundColor.a <= FullAlphaThreshold)
            {
                return;
            }
            for (var segmentIndex = 0; segmentIndex < _segmentCount; segmentIndex++)
            {
                AddSolidRect(vertexHelper, _segmentRects[segmentIndex], _style.BackgroundColor);
            }
        }
        private void AddFilledSegments(VertexHelper vertexHelper)
        {
            for (var segmentIndex = 0; segmentIndex < _segmentCount; segmentIndex++)
            {
                var fillAmount = GetSegmentFillAmount(segmentIndex);
                if (fillAmount <= EmptyProgress)
                {
                    continue;
                }
                var filledRect = GetFilledRect(_segmentRects[segmentIndex], fillAmount);
                if (filledRect.width <= 0f || filledRect.height <= 0f)
                {
                    continue;
                }
                if (UsesGradientFill())
                {
                    AddGradientRect(vertexHelper, filledRect);
                    continue;
                }
                AddSolidRect(vertexHelper, filledRect, EvaluateFillColor(0f));
            }
        }
        private void AddSeparators(VertexHelper vertexHelper)
        {
            if (_segmentCount <= 1 || _style.SeparatorThickness <= 0f || _style.SeparatorColor.a <= FullAlphaThreshold)
            {
                return;
            }
            var segmentLength = _style.Vertical ? _segmentRects[0].height : _segmentRects[0].width;
            if (segmentLength < SegmentVisibilityThresholdPixels)
            {
                // 1px 未満の境界線は視認できず、頂点だけ増やしても描画品質に寄与しないため省く。
                return;
            }
            for (var segmentIndex = 0; segmentIndex < _segmentCount - 1; segmentIndex++)
            {
                AddSeparator(vertexHelper, segmentIndex);
            }
        }
        private void AddSeparator(VertexHelper vertexHelper, int segmentIndex)
        {
            var currentRect = _segmentRects[segmentIndex];
            if (_style.Vertical)
            {
                var centerY = currentRect.yMax + (_style.SegmentSpacing * 0.5f);
                var separatorRect = new Rect(currentRect.xMin, centerY - (_style.SeparatorThickness * 0.5f), currentRect.width, _style.SeparatorThickness);
                AddSolidRect(vertexHelper, separatorRect, _style.SeparatorColor);
                return;
            }
            var centerX = currentRect.xMax + (_style.SegmentSpacing * 0.5f);
            var lineRect = new Rect(centerX - (_style.SeparatorThickness * 0.5f), currentRect.yMin, _style.SeparatorThickness, currentRect.height);
            AddSolidRect(vertexHelper, lineRect, _style.SeparatorColor);
        }
        private void AddOutline(VertexHelper vertexHelper, Rect rect, Color color, float thickness)
        {
            if (thickness <= 0f || color.a <= FullAlphaThreshold)
            {
                return;
            }
            var halfThickness = thickness * 0.5f;
            AddOutlineRect(vertexHelper, new Rect(rect.xMin, rect.yMax - halfThickness, rect.width, thickness), color);
            AddOutlineRect(vertexHelper, new Rect(rect.xMin, rect.yMin - halfThickness, rect.width, thickness), color);
            AddOutlineRect(vertexHelper, new Rect(rect.xMin - halfThickness, rect.yMin, thickness, rect.height), color);
            AddOutlineRect(vertexHelper, new Rect(rect.xMax - halfThickness, rect.yMin, thickness, rect.height), color);
        }
        private void AddOutlineRect(VertexHelper vertexHelper, Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            AddSolidRectInternal(vertexHelper, rect, color, _outlineQuadVertices);
        }
        private float GetSegmentFillAmount(int index)
        {
            var totalFilledSegments = _normalizedValue * _segmentCount;
            var wholeFilledSegments = Mathf.FloorToInt(totalFilledSegments);
            if (index < wholeFilledSegments)
            {
                return FullProgress;
            }
            if (index > wholeFilledSegments)
            {
                return EmptyProgress;
            }
            return totalFilledSegments - wholeFilledSegments;
        }
        private Rect GetFilledRect(Rect segmentRect, float fillAmount)
        {
            if (fillAmount <= EmptyProgress)
            {
                return new Rect(segmentRect.xMin, segmentRect.yMin, 0f, 0f);
            }
            if (_style.Vertical)
            {
                return new Rect(segmentRect.xMin, segmentRect.yMin, segmentRect.width, segmentRect.height * fillAmount);
            }
            return new Rect(segmentRect.xMin, segmentRect.yMin, segmentRect.width * fillAmount, segmentRect.height);
        }
        private bool UsesGradientFill()
        {
            return !IsUnsetColor(_style.FillStartColor) || !IsUnsetColor(_style.FillEndColor);
        }
        private Color EvaluateFillColor(float normalizedHorizontalPosition)
        {
            var clampedPosition = Mathf.Clamp01(normalizedHorizontalPosition);
            var baseColor = UsesGradientFill() ? Color.Lerp(_style.FillStartColor, _style.FillEndColor, clampedPosition) : _style.FillColor;
            return Color.Lerp(baseColor, _style.GlowColor, _glowIntensity);
        }
        private void AddGradientRect(VertexHelper vertexHelper, Rect rect)
        {
            var totalRect = rectTransform.rect;
            var width = Mathf.Max(1f, totalRect.width);
            var leftPosition = (rect.xMin - totalRect.xMin) / width;
            var rightPosition = (rect.xMax - totalRect.xMin) / width;
            _quadVertices[0] = CreateVertex(new Vector2(rect.xMin, rect.yMin), EvaluateFillColor(leftPosition));
            _quadVertices[1] = CreateVertex(new Vector2(rect.xMin, rect.yMax), EvaluateFillColor(leftPosition));
            _quadVertices[2] = CreateVertex(new Vector2(rect.xMax, rect.yMax), EvaluateFillColor(rightPosition));
            _quadVertices[3] = CreateVertex(new Vector2(rect.xMax, rect.yMin), EvaluateFillColor(rightPosition));
            vertexHelper.AddUIVertexQuad(_quadVertices);
        }
        private void AddSolidRect(VertexHelper vertexHelper, Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f || color.a <= FullAlphaThreshold)
            {
                return;
            }
            AddSolidRectInternal(vertexHelper, rect, color, _quadVertices);
        }
        private static void AddSolidRectInternal(VertexHelper vertexHelper, Rect rect, Color color, UIVertex[] vertices)
        {
            vertices[0] = CreateVertex(new Vector2(rect.xMin, rect.yMin), color);
            vertices[1] = CreateVertex(new Vector2(rect.xMin, rect.yMax), color);
            vertices[2] = CreateVertex(new Vector2(rect.xMax, rect.yMax), color);
            vertices[3] = CreateVertex(new Vector2(rect.xMax, rect.yMin), color);
            vertexHelper.AddUIVertexQuad(vertices);
        }
        private static bool IsUnsetColor(Color color)
        {
            return color.r == 0f && color.g == 0f && color.b == 0f && color.a == 0f;
        }
        private static UIVertex CreateVertex(Vector2 position, Color color)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            return vertex;
        }
        internal float NormalizedValue { get => _normalizedValue; set => _normalizedValue = value; }
        internal float GlowIntensity { get => _glowIntensity; set => _glowIntensity = value; }
        internal Vector2 AnchoredPosition { get => rectTransform.anchoredPosition; set => rectTransform.anchoredPosition = value; }
        internal void RequestRedraw() { SetVerticesDirty(); }
        internal void SetAnimating(bool isAnimating) { IsAnimating = isAnimating; }
    }
}
