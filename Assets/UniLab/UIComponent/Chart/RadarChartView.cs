using System;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI
{
    /// <summary>
    /// N 軸の値を 1 枚のメッシュで描画する汎用レーダーチャート。
    /// 初期化時にだけバッファを確保し、描画ホットパスでは再確保しない。
    /// </summary>
    // Graphic の RequireComponent は派生クラスに継承されないため、AddComponent 経路でも CanvasRenderer を保証する
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadarChartView : MaskableGraphic
    {
        private const int MinAxisCount = 3;
        private const int MaxAxisCount = 12;
        private const float FullCircleDegrees = 360f;
        private const float MinimumAnimationDurationSeconds = 0.0001f;
        private const float OutBackOvershoot = 1.70158f;

        // perf: 描画で使う配列は Initialize でだけ確保し、OnPopulateMesh 中の GC を防ぐ。
        private float[] _normalizedValues = Array.Empty<float>();
        private float[] _animationStartValues = Array.Empty<float>();
        private float[] _animationTargetValues = Array.Empty<float>();
        private Vector2[] _unitDirectionVectors = Array.Empty<Vector2>();
        private Vector2[] _outerVertices = Array.Empty<Vector2>();
        private Vector2[] _valueVertices = Array.Empty<Vector2>();
        private Color[] _axisColors = Array.Empty<Color>();

        // perf: 線分四辺形の一時頂点を使い回し、AddUIVertexQuad ごとの配列確保を防ぐ。
        private readonly UIVertex[] _lineQuadVertices = new UIVertex[4];

        private int _axisCount;
        private bool _hasCustomAxisColors;
        private RadarChartStyle _style = RadarChartStyle.Default;
        private IDisposable _animationSubscription;
        private float _animationStartedAtRealtimeSeconds;
        private float _animationDurationSeconds;
        private RadarChartEasing _animationEasing;

        /// <summary>
        /// 値アニメーションの再生中かを返す。
        /// </summary>
        public bool IsAnimating { get; private set; }

        /// <summary>
        /// 軸数を確定し、描画と値保持に必要な内部バッファを確保する。
        /// </summary>
        public void Initialize(int axisCount)
        {
            if (axisCount < MinAxisCount || axisCount > MaxAxisCount)
            {
                throw new ArgumentOutOfRangeException(nameof(axisCount), axisCount, $"軸数は {MinAxisCount}〜{MaxAxisCount} の範囲で指定すべき。");
            }

            StopAnimation();
            _axisCount = axisCount;
            _normalizedValues = new float[axisCount];
            _animationStartValues = new float[axisCount];
            _animationTargetValues = new float[axisCount];
            _unitDirectionVectors = new Vector2[axisCount];
            _outerVertices = new Vector2[axisCount];
            _valueVertices = new Vector2[axisCount];
            _axisColors = new Color[axisCount];
            _hasCustomAxisColors = false;

            UpdateUnitDirectionVectors();
            SetVerticesDirty();
        }

        /// <summary>
        /// 各軸の正規化済み値を更新する。入力値は 0〜1 に Clamp して保持する。
        /// </summary>
        public void SetValues(ReadOnlySpan<float> normalizedValues)
        {
            EnsureInitialized();
            StopAnimation();
            CopyClampedValues(normalizedValues, _normalizedValues);
            SetVerticesDirty();
        }

        /// <summary>
        /// 現在値から指定値まで補間し、毎フレーム再描画する。
        /// </summary>
        public void AnimateTo(ReadOnlySpan<float> targetValues, float durationSeconds, RadarChartEasing easing = RadarChartEasing.OutBack)
        {
            EnsureInitialized();
            CopyClampedValues(targetValues, _animationTargetValues);
            StartAnimation(durationSeconds, easing, zeroStart: false);
        }

        /// <summary>
        /// 全軸を中心から現在値まで伸ばす演出を再生する。
        /// </summary>
        public void PlayGrowFromCenter(float durationSeconds)
        {
            EnsureInitialized();

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                _animationTargetValues[axisIndex] = _normalizedValues[axisIndex];
            }

            StartAnimation(durationSeconds, RadarChartEasing.OutBack, zeroStart: true);
        }

        /// <summary>
        /// 描画スタイルを差し替え、必要な頂点方向キャッシュを更新する。
        /// </summary>
        public void SetStyle(RadarChartStyle style)
        {
            _style = style;

            if (_axisCount > 0)
            {
                UpdateUnitDirectionVectors();
            }

            SetVerticesDirty();
        }

        /// <summary>
        /// 各軸の描画色を設定する。
        /// </summary>
        public void SetAxisColors(ReadOnlySpan<Color> axisColors)
        {
            EnsureInitialized();

            if (axisColors.Length != _axisCount)
            {
                throw new ArgumentException($"軸色の数は軸数 {_axisCount} と一致すべき。", nameof(axisColors));
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                _axisColors[axisIndex] = axisColors[axisIndex];
            }

            _hasCustomAxisColors = true;
            SetVerticesDirty();
        }

        /// <summary>
        /// 指定軸の外周方向にある局所座標を返す。ラベル配置に使う。
        /// </summary>
        public Vector2 GetVertexLocalPosition(int axisIndex, float radiusScale)
        {
            EnsureInitialized();

            if (axisIndex < 0 || axisIndex >= _axisCount)
            {
                throw new ArgumentOutOfRangeException(nameof(axisIndex), axisIndex, "軸インデックスが範囲外。");
            }

            var rect = rectTransform.rect;
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            return center + (_unitDirectionVectors[axisIndex] * radius * radiusScale);
        }

        /// <summary>
        /// 内部バッファに保持した値から UI メッシュを再構築する。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            if (_axisCount < MinAxisCount)
            {
                return;
            }

            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f)
            {
                return;
            }

            var center = rect.center;
            UpdatePolygonVertices(center, radius);

            AddPolygonFill(vertexHelper, center, _outerVertices, _style.BackgroundColor);
            AddAxisLines(vertexHelper, center);
            AddValuePolygonFill(vertexHelper, center);
            AddValuePolygonOutline(vertexHelper);
            AddPolygonOutline(vertexHelper, _outerVertices, _style.OutlineColor, _style.OutlineThickness);
        }

        protected override void OnDestroy()
        {
            StopAnimation();
            base.OnDestroy();
        }

        private void EnsureInitialized()
        {
            if (_axisCount < MinAxisCount)
            {
                throw new InvalidOperationException("Initialize を先に呼ぶべき。");
            }
        }

        private void CopyClampedValues(ReadOnlySpan<float> sourceValues, float[] destinationValues)
        {
            if (sourceValues.Length != _axisCount)
            {
                throw new ArgumentException($"値の数は軸数 {_axisCount} と一致すべき。", nameof(sourceValues));
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                destinationValues[axisIndex] = Mathf.Clamp01(sourceValues[axisIndex]);
            }
        }

        private void StartAnimation(float durationSeconds, RadarChartEasing easing, bool zeroStart)
        {
            StopAnimation();

            if (durationSeconds <= 0f || easing == RadarChartEasing.None)
            {
                ApplyAnimationValues(1f, zeroStart);
                return;
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                _animationStartValues[axisIndex] = zeroStart ? 0f : _normalizedValues[axisIndex];
            }

            if (zeroStart)
            {
                for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
                {
                    _normalizedValues[axisIndex] = 0f;
                }

                SetVerticesDirty();
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
            ApplyAnimationValues(EvaluateEasing(normalizedTime, _animationEasing), zeroStart: false);

            if (normalizedTime < 1f)
            {
                return;
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                _normalizedValues[axisIndex] = _animationTargetValues[axisIndex];
            }

            StopAnimation();
            SetVerticesDirty();
        }

        private void ApplyAnimationValues(float easedProgress, bool zeroStart)
        {
            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                var startValue = zeroStart ? 0f : _animationStartValues[axisIndex];
                _normalizedValues[axisIndex] = Mathf.Clamp01(Mathf.LerpUnclamped(startValue, _animationTargetValues[axisIndex], easedProgress));
            }

            SetVerticesDirty();
        }

        private void StopAnimation()
        {
            _animationSubscription?.Dispose();
            _animationSubscription = null;
            IsAnimating = false;
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
                        var inverse = normalizedTime - 1f;
                        return 1f + ((OutBackOvershoot + 1f) * inverse * inverse * inverse) + (OutBackOvershoot * inverse * inverse);
                    }
                default:
                    return normalizedTime;
            }
        }

        private void UpdateUnitDirectionVectors()
        {
            var signedStepDegrees = FullCircleDegrees / _axisCount;
            if (_style.Clockwise)
            {
                signedStepDegrees *= -1f;
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                var angleDegrees = _style.StartAngleDegrees + (signedStepDegrees * axisIndex);
                var angleRadians = angleDegrees * Mathf.Deg2Rad;
                _unitDirectionVectors[axisIndex] = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
            }
        }

        private void UpdatePolygonVertices(Vector2 center, float radius)
        {
            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                var direction = _unitDirectionVectors[axisIndex];
                _outerVertices[axisIndex] = center + (direction * radius);
                _valueVertices[axisIndex] = center + (direction * radius * _normalizedValues[axisIndex]);
            }
        }

        private void AddAxisLines(VertexHelper vertexHelper, Vector2 center)
        {
            if (_style.AxisLineThickness <= 0f || _style.AxisLineColor.a <= 0f)
            {
                return;
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                var axisColor = _hasCustomAxisColors ? GetAxisLineColor(axisIndex) : _style.AxisLineColor;
                AddLineQuad(vertexHelper, center, _outerVertices[axisIndex], axisColor, axisColor, _style.AxisLineThickness);
            }
        }

        private void AddPolygonFill(VertexHelper vertexHelper, Vector2 center, Vector2[] polygonVertices, Color color)
        {
            if (color.a <= 0f)
            {
                return;
            }

            var centerVertexIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(CreateVertex(center, color));

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                vertexHelper.AddVert(CreateVertex(polygonVertices[axisIndex], color));
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                var currentVertexIndex = centerVertexIndex + 1 + axisIndex;
                var nextVertexIndex = centerVertexIndex + 1 + ((axisIndex + 1) % _axisCount);
                vertexHelper.AddTriangle(centerVertexIndex, currentVertexIndex, nextVertexIndex);
            }
        }

        private void AddValuePolygonFill(VertexHelper vertexHelper, Vector2 center)
        {
            if (ShouldUseRadialFillGradient())
            {
                AddGradientPolygonFill(vertexHelper, center, _valueVertices, _style.FillCenterColor, _style.FillEdgeColor);
                return;
            }

            if (!_hasCustomAxisColors)
            {
                AddPolygonFill(vertexHelper, center, _valueVertices, _style.FillColor);
                return;
            }

            AddGradientPolygonFill(vertexHelper, center, _valueVertices, _style.FillColor, default);
        }

        private void AddValuePolygonOutline(VertexHelper vertexHelper)
        {
            if (!_hasCustomAxisColors)
            {
                AddPolygonOutline(vertexHelper, _valueVertices, _style.ValueOutlineColor, _style.ValueOutlineThickness);
                return;
            }

            if (_style.ValueOutlineThickness <= 0f)
            {
                return;
            }

            var outlineAlpha = _style.ValueOutlineColor.a;
            if (outlineAlpha <= 0f)
            {
                return;
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                var nextAxisIndex = (axisIndex + 1) % _axisCount;
                AddLineQuad(
                    vertexHelper,
                    _valueVertices[axisIndex],
                    _valueVertices[nextAxisIndex],
                    GetColorWithAlpha(_axisColors[axisIndex], outlineAlpha),
                    GetColorWithAlpha(_axisColors[nextAxisIndex], outlineAlpha),
                    _style.ValueOutlineThickness);
            }
        }

        private void AddGradientPolygonFill(VertexHelper vertexHelper, Vector2 center, Vector2[] polygonVertices, Color centerColor, Color edgeColor)
        {
            var centerVertexIndex = vertexHelper.currentVertCount;
            var fillCenterColor = _hasCustomAxisColors && !ShouldUseRadialFillGradient() ? _style.FillColor : centerColor;
            if (fillCenterColor.a <= 0f && !_hasCustomAxisColors)
            {
                return;
            }

            vertexHelper.AddVert(CreateVertex(center, fillCenterColor));

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                var outerColor = ResolveFillOuterColor(axisIndex, edgeColor);
                vertexHelper.AddVert(CreateVertex(polygonVertices[axisIndex], outerColor));
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                var currentVertexIndex = centerVertexIndex + 1 + axisIndex;
                var nextVertexIndex = centerVertexIndex + 1 + ((axisIndex + 1) % _axisCount);
                vertexHelper.AddTriangle(centerVertexIndex, currentVertexIndex, nextVertexIndex);
            }
        }

        private Color ResolveFillOuterColor(int axisIndex, Color edgeColor)
        {
            if (!_hasCustomAxisColors)
            {
                return edgeColor;
            }

            if (ShouldUseRadialFillGradient())
            {
                return MultiplyColors(edgeColor, _axisColors[axisIndex]);
            }

            return _axisColors[axisIndex];
        }

        private bool ShouldUseRadialFillGradient()
        {
            return !IsUnsetColor(_style.FillCenterColor) || !IsUnsetColor(_style.FillEdgeColor);
        }

        private static bool IsUnsetColor(Color color)
        {
            return color.r == 0f && color.g == 0f && color.b == 0f && color.a == 0f;
        }

        private Color GetAxisLineColor(int axisIndex)
        {
            var axisColor = _axisColors[axisIndex];
            axisColor.a = _style.AxisLineColor.a;
            return axisColor;
        }

        private static Color MultiplyColors(Color left, Color right)
        {
            return new Color(
                left.r * right.r,
                left.g * right.g,
                left.b * right.b,
                left.a * right.a);
        }

        private static Color GetColorWithAlpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        private void AddPolygonOutline(VertexHelper vertexHelper, Vector2[] polygonVertices, Color color, float thickness)
        {
            if (thickness <= 0f || color.a <= 0f)
            {
                return;
            }

            for (var axisIndex = 0; axisIndex < _axisCount; axisIndex++)
            {
                var nextAxisIndex = (axisIndex + 1) % _axisCount;
                AddLineQuad(vertexHelper, polygonVertices[axisIndex], polygonVertices[nextAxisIndex], color, color, thickness);
            }
        }

        private void AddLineQuad(VertexHelper vertexHelper, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness)
        {
            var segment = end - start;
            var magnitude = segment.magnitude;
            if (magnitude <= Mathf.Epsilon)
            {
                return;
            }

            var normal = new Vector2(-segment.y / magnitude, segment.x / magnitude) * (thickness * 0.5f);

            _lineQuadVertices[0] = CreateVertex(start - normal, startColor);
            _lineQuadVertices[1] = CreateVertex(start + normal, startColor);
            _lineQuadVertices[2] = CreateVertex(end + normal, endColor);
            _lineQuadVertices[3] = CreateVertex(end - normal, endColor);
            vertexHelper.AddUIVertexQuad(_lineQuadVertices);
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
