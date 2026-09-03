using System.Reflection;
using NUnit.Framework;
using UniLab.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.Tests.EditMode.UI
{
    /// <summary>
    /// RadarChartView の座標計算と値 Clamp を検証する EditMode テスト。
    /// </summary>
    public class RadarChartViewTest
    {
        private const float ViewSize = 200f;

        private GameObject _gameObject;
        private RadarChartView _radarChartView;

        /// <summary>
        /// 先頭軸は StartAngle 90 度で真上の頂点を返す。
        /// </summary>
        [Test]
        public void GetVertexLocalPosition_AxisZeroWithStartAngle90_ReturnsTopVertex()
        {
            CreateView();
            _radarChartView.Initialize(5);
            _radarChartView.SetStyle(RadarChartStyle.Default);

            var vertexPosition = _radarChartView.GetVertexLocalPosition(0, 1f);

            Assert.That(vertexPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(vertexPosition.y, Is.EqualTo(ViewSize * 0.5f).Within(0.001f));
        }

        /// <summary>
        /// SetValues は 0〜1 の範囲外入力を Clamp して保持する。
        /// </summary>
        [Test]
        public void SetValues_OutOfRangeValues_AreClamped()
        {
            CreateView();
            _radarChartView.Initialize(3);

            _radarChartView.SetValues(stackalloc float[] { -0.5f, 0.25f, 1.5f });

            var normalizedValuesField = typeof(RadarChartView).GetField("_normalizedValues", BindingFlags.NonPublic | BindingFlags.Instance);
            var normalizedValues = (float[])normalizedValuesField!.GetValue(_radarChartView);

            Assert.That(normalizedValues[0], Is.EqualTo(0f).Within(0.001f));
            Assert.That(normalizedValues[1], Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(normalizedValues[2], Is.EqualTo(1f).Within(0.001f));
        }

        /// <summary>
        /// SetAxisColors は軸数と異なる要素数を拒否する。
        /// </summary>
        [Test]
        public void SetAxisColors_LengthMismatch_ThrowsArgumentException()
        {
            CreateView();
            _radarChartView.Initialize(4);
            var axisColors = new[] { Color.red, Color.green, Color.blue };

            Assert.That(
                () => _radarChartView.SetAxisColors(axisColors),
                Throws.ArgumentException);
        }

        /// <summary>
        /// 放射グラデーション指定時は中心頂点色と外周頂点色が異なる。
        /// </summary>
        [Test]
        public void OnPopulateMesh_WithRadialGradient_UsesDifferentCenterAndOuterVertexColors()
        {
            CreateView();
            _radarChartView.Initialize(3);
            _radarChartView.SetValues(stackalloc float[] { 1f, 0.75f, 0.5f });
            _radarChartView.SetStyle(new RadarChartStyle(
                outlineColor: Color.clear,
                outlineThickness: 0f,
                axisLineColor: Color.clear,
                axisLineThickness: 0f,
                fillColor: new Color(0.4f, 0.4f, 0.4f, 0.5f),
                fillCenterColor: new Color(1f, 0f, 0f, 0.6f),
                fillEdgeColor: new Color(0f, 0f, 1f, 0.3f),
                valueOutlineColor: Color.clear,
                valueOutlineThickness: 0f,
                backgroundColor: Color.clear));

            using var vertexHelper = new VertexHelper();
            InvokePopulateMesh(vertexHelper);

            Assert.That(vertexHelper.currentVertCount, Is.EqualTo(4));

            var centerVertex = new UIVertex();
            var outerVertex = new UIVertex();
            vertexHelper.PopulateUIVertex(ref centerVertex, 0);
            vertexHelper.PopulateUIVertex(ref outerVertex, 1);

            Assert.That(centerVertex.color, Is.Not.EqualTo(outerVertex.color));
        }

        /// <summary>
        /// 各テストで生成した GameObject を破棄する。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        private void CreateView()
        {
            _gameObject = new GameObject("RadarChartView", typeof(RectTransform), typeof(CanvasRenderer));
            var rectTransform = _gameObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ViewSize);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ViewSize);
            _radarChartView = _gameObject.AddComponent<RadarChartView>();
        }

        private void InvokePopulateMesh(VertexHelper vertexHelper)
        {
            var method = typeof(RadarChartView).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(_radarChartView, new object[] { vertexHelper });
        }
    }
}
