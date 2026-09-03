using System;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI
{
    /// <summary>
    /// 基準線から左右に伸びる差分バーを 1 枚のメッシュで描画する部品。
    /// 呼び出し側が差分ラベルを置けるよう、先端座標も返す。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DeltaBarView : MaskableGraphic
    {
        private const float MinimumAnimationDurationSeconds = 0.0001f;
        private const float FullProgress = 1f;
        private const float FullAlphaThreshold = 0f;

        private readonly UIVertex[] _quadVertices = new UIVertex[4];
        private readonly UIVertex[] _outlineQuadVertices = new UIVertex[4];

        private float _signedNormalizedValue;
        private bool _isInitialized;
        private DeltaBarStyle _style = DeltaBarStyle.Default;
        private IDisposable _animationSubscription;
        private float _animationStartedAtRealtimeSeconds;
        private float _animationDurationSeconds;
        private float _animationStartValue;
        private float _animationTargetValue;
        private RadarChartEasing _animationEasing;

        /// <summary>
        /// 値アニメーションの再生中かを返す。
        /// </summary>
        public bool IsAnimating { get; private set; }

        /// <summary>
        /// 単一バーを描くための初期状態を確定する。
        /// 呼び出し順で落とさないため、値設定前の暗黙初期化と同じ状態に揃える。
        /// </summary>
        public void Initialize()
        {
            StopAnimation();
            _signedNormalizedValue = Mathf.Clamp(_signedNormalizedValue, -1f, 1f);
            _isInitialized = true;
            SetVerticesDirty();
        }

        /// <summary>
        /// 正負付きの正規化済み値を更新する。
        /// </summary>
        public void SetValue(float signedNormalizedValue)
        {
            EnsureInitialized();
            StopAnimation();
            _signedNormalizedValue = Mathf.Clamp(signedNormalizedValue, -1f, 1f);
            SetVerticesDirty();
        }

        /// <summary>
        /// 描画スタイルを差し替える。
        /// </summary>
        public void SetStyle(DeltaBarStyle style)
        {
            _style = style;
            SetVerticesDirty();
        }

        /// <summary>
        /// 現在値から指定値まで補間し、毎フレーム再描画する。
        /// </summary>
        public void AnimateTo(float signedNormalizedValue, float durationSeconds, RadarChartEasing easing)
        {
            EnsureInitialized();
            _animationStartValue = _signedNormalizedValue;
            _animationTargetValue = Mathf.Clamp(signedNormalizedValue, -1f, 1f);
            StartAnimation(durationSeconds, easing);
        }

        /// <summary>
        /// バーの先端局所座標を返す。差分ラベルの配置を呼び出し側で制御するために使う。
        /// </summary>
        public Vector2 GetBarEndLocalPosition()
        {
            EnsureInitialized();
            return CalculateBarEndLocalPosition(rectTransform.rect, _signedNormalizedValue);
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

            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            AddSolidRect(vertexHelper, rect, _style.BackgroundColor, _quadVertices);
            AddDeltaBar(vertexHelper, rect);
            AddBaseline(vertexHelper, rect);
            AddOutline(vertexHelper, rect, _style.OutlineColor, _style.OutlineThickness);
        }

        protected override void OnDestroy()
        {
            StopAnimation();
            base.OnDestroy();
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            Initialize();
        }

        private void StartAnimation(float durationSeconds, RadarChartEasing easing)
        {
            StopAnimation();

            if (durationSeconds <= 0f || easing == RadarChartEasing.None)
            {
                _signedNormalizedValue = _animationTargetValue;
                SetVerticesDirty();
                return;
            }

            _animationStartedAtRealtimeSeconds = Time.realtimeSinceStartup;
            _animationDurationSeconds = Mathf.Max(MinimumAnimationDurationSeconds, durationSeconds);
            _animationEasing = easing;
            IsAnimating = true;
            _animationSubscription = Observable.EveryUpdate(destroyCancellationToken)
                .Subscribe(_ => AdvanceAnimation());
        }

        private void AdvanceAnimation()
        {
            if (!IsAnimating)
            {
                return;
            }

            var elapsedSeconds = Time.realtimeSinceStartup - _animationStartedAtRealtimeSeconds;
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / _animationDurationSeconds);
            var easedProgress = EvaluateEasing(normalizedTime, _animationEasing);
            _signedNormalizedValue = Mathf.Clamp(Mathf.LerpUnclamped(_animationStartValue, _animationTargetValue, easedProgress), -1f, 1f);
            SetVerticesDirty();

            if (normalizedTime < FullProgress)
            {
                return;
            }

            _signedNormalizedValue = _animationTargetValue;
            StopAnimation();
            SetVerticesDirty();
        }

        private void StopAnimation()
        {
            _animationSubscription?.Dispose();
            _animationSubscription = null;
            IsAnimating = false;
        }

        private void AddDeltaBar(VertexHelper vertexHelper, Rect rect)
        {
            if (Mathf.Approximately(_signedNormalizedValue, 0f))
            {
                return;
            }

            var baselineX = GetBaselineX(rect);
            var maxLeftLength = baselineX - rect.xMin;
            var maxRightLength = rect.xMax - baselineX;
            var centerY = rect.center.y;
            var barHeight = rect.height;

            if (_signedNormalizedValue > 0f)
            {
                var width = maxRightLength * _signedNormalizedValue;
                AddSolidRect(vertexHelper, new Rect(baselineX, centerY - (barHeight * 0.5f), width, barHeight), _style.PositiveColor, _quadVertices);
                return;
            }

            var negativeWidth = maxLeftLength * Mathf.Abs(_signedNormalizedValue);
            AddSolidRect(vertexHelper, new Rect(baselineX - negativeWidth, centerY - (barHeight * 0.5f), negativeWidth, barHeight), _style.NegativeColor, _quadVertices);
        }

        private void AddBaseline(VertexHelper vertexHelper, Rect rect)
        {
            if (_style.BaselineThickness <= 0f)
            {
                return;
            }

            var baselineColor = Mathf.Approximately(_signedNormalizedValue, 0f) ? _style.ZeroColor : _style.BaselineColor;
            if (baselineColor.a <= FullAlphaThreshold)
            {
                return;
            }

            var baselineX = GetBaselineX(rect);
            var baselineRect = new Rect(
                baselineX - (_style.BaselineThickness * 0.5f),
                rect.yMin,
                _style.BaselineThickness,
                rect.height);
            AddSolidRect(vertexHelper, baselineRect, baselineColor, _quadVertices);
        }

        private void AddOutline(VertexHelper vertexHelper, Rect rect, Color color, float thickness)
        {
            if (thickness <= 0f || color.a <= FullAlphaThreshold)
            {
                return;
            }

            var halfThickness = thickness * 0.5f;
            AddSolidRect(vertexHelper, new Rect(rect.xMin, rect.yMax - halfThickness, rect.width, thickness), color, _outlineQuadVertices);
            AddSolidRect(vertexHelper, new Rect(rect.xMin, rect.yMin - halfThickness, rect.width, thickness), color, _outlineQuadVertices);
            AddSolidRect(vertexHelper, new Rect(rect.xMin - halfThickness, rect.yMin, thickness, rect.height), color, _outlineQuadVertices);
            AddSolidRect(vertexHelper, new Rect(rect.xMax - halfThickness, rect.yMin, thickness, rect.height), color, _outlineQuadVertices);
        }

        private Vector2 CalculateBarEndLocalPosition(Rect rect, float signedNormalizedValue)
        {
            var baselineX = GetBaselineX(rect);
            var centerY = rect.center.y;
            if (Mathf.Approximately(signedNormalizedValue, 0f))
            {
                return new Vector2(baselineX, centerY);
            }

            if (signedNormalizedValue > 0f)
            {
                var maxRightLength = rect.xMax - baselineX;
                return new Vector2(baselineX + (maxRightLength * signedNormalizedValue), centerY);
            }

            var maxLeftLength = baselineX - rect.xMin;
            return new Vector2(baselineX - (maxLeftLength * Mathf.Abs(signedNormalizedValue)), centerY);
        }

        private float GetBaselineX(Rect rect)
        {
            return Mathf.Lerp(rect.xMin, rect.xMax, _style.BaselinePosition);
        }

        private static void AddSolidRect(VertexHelper vertexHelper, Rect rect, Color color, UIVertex[] vertices)
        {
            if (rect.width <= 0f || rect.height <= 0f || color.a <= FullAlphaThreshold)
            {
                return;
            }

            vertices[0] = CreateVertex(new Vector2(rect.xMin, rect.yMin), color);
            vertices[1] = CreateVertex(new Vector2(rect.xMin, rect.yMax), color);
            vertices[2] = CreateVertex(new Vector2(rect.xMax, rect.yMax), color);
            vertices[3] = CreateVertex(new Vector2(rect.xMax, rect.yMin), color);
            vertexHelper.AddUIVertexQuad(vertices);
        }

        private static float EvaluateEasing(float normalizedTime, RadarChartEasing easing)
        {
            switch (easing)
            {
                case RadarChartEasing.None:
                case RadarChartEasing.Linear:
                    return normalizedTime;
                case RadarChartEasing.OutCubic:
                    return 1f - Mathf.Pow(1f - normalizedTime, 3f);
                case RadarChartEasing.OutBack:
                    {
                        const float outBackOvershoot = 1.70158f;
                        var inverse = normalizedTime - 1f;
                        return 1f + ((outBackOvershoot + 1f) * inverse * inverse * inverse) + (outBackOvershoot * inverse * inverse);
                    }
                default:
                    return normalizedTime;
            }
        }

        private static UIVertex CreateVertex(Vector2 position, Color color)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            return vertex;
        }
    }
}
