using System.Reflection;
using NUnit.Framework;
using UniLab.UI;
using UnityEngine;

namespace UniLab.Tests.EditMode.UI
{
    /// <summary>
    /// SegmentedBarView の座標計算と部分充填を検証する EditMode テスト。
    /// </summary>
    public class SegmentedBarViewTest
    {
        private const float ViewWidth = 200f;
        private const float ViewHeight = 20f;
        private const float Epsilon = 0.001f;

        private GameObject _gameObject;
        private SegmentedBarView _segmentedBarView;

        /// <summary>
        /// Initialize 後の矩形計算は等間隔セグメントを返す。
        /// </summary>
        [Test]
        public void GetSegmentLocalRect_AfterInitialize_ReturnsExpectedRect()
        {
            CreateView();
            _segmentedBarView.Initialize(4);
            _segmentedBarView.SetStyle(new SegmentedBarStyle(
                fillColor: Color.white,
                fillStartColor: Color.clear,
                fillEndColor: Color.clear,
                backgroundColor: Color.black,
                glowColor: Color.white,
                separatorColor: Color.clear,
                separatorThickness: 0f,
                outlineColor: Color.clear,
                outlineThickness: 0f,
                segmentSpacing: 4f));

            var segmentRect = _segmentedBarView.GetSegmentLocalRect(1);

            Assert.That(segmentRect.xMin, Is.EqualTo(-49f).Within(Epsilon));
            Assert.That(segmentRect.width, Is.EqualTo(47f).Within(Epsilon));
            Assert.That(segmentRect.height, Is.EqualTo(ViewHeight).Within(Epsilon));
        }

        /// <summary>
        /// 範囲外入力でも部分充填の矩形は 0〜1 に収まる。
        /// </summary>
        [Test]
        public void SetValue_OutOfRangeValue_ClampsFilledRect()
        {
            CreateView();
            _segmentedBarView.Initialize(5);

            _segmentedBarView.SetValue(1.5f);

            var segmentRect = _segmentedBarView.GetSegmentLocalRect(4);
            var filledRect = _segmentedBarView.GetFilledSegmentLocalRect(4);
            Assert.That(filledRect.width, Is.EqualTo(segmentRect.width).Within(Epsilon));

            _segmentedBarView.SetValue(-0.5f);

            var emptyRect = _segmentedBarView.GetFilledSegmentLocalRect(0);
            Assert.That(emptyRect.width, Is.EqualTo(0f).Within(Epsilon));
        }

        /// <summary>
        /// 初期化前に値を渡しても暗黙初期化で例外を避ける。
        /// </summary>
        [Test]
        public void SetValue_BeforeInitialize_DoesNotThrow()
        {
            CreateView();

            Assert.That(() => _segmentedBarView.SetValue(0.5f), Throws.Nothing);

            var segmentRect = _segmentedBarView.GetSegmentLocalRect(0);
            Assert.That(segmentRect.width, Is.EqualTo(ViewWidth).Within(Epsilon));
        }

        /// <summary>
        /// 0.74 / 10 は 7 セグメント全充填と 8 個目の 40% 充填になる。
        /// </summary>
        [Test]
        public void GetFilledSegmentLocalRect_ValuePointSevenFourOfTen_ReturnsSevenFullAndOnePartial()
        {
            CreateView();
            _segmentedBarView.Initialize(10);
            _segmentedBarView.SetStyle(new SegmentedBarStyle(
                fillColor: Color.white,
                fillStartColor: Color.clear,
                fillEndColor: Color.clear,
                backgroundColor: Color.black,
                glowColor: Color.white,
                separatorColor: Color.clear,
                separatorThickness: 0f,
                outlineColor: Color.clear,
                outlineThickness: 0f,
                segmentSpacing: 0f));
            _segmentedBarView.SetValue(0.74f);

            for (var segmentIndex = 0; segmentIndex < 7; segmentIndex++)
            {
                var segmentRect = _segmentedBarView.GetSegmentLocalRect(segmentIndex);
                var filledRect = _segmentedBarView.GetFilledSegmentLocalRect(segmentIndex);
                Assert.That(filledRect.width, Is.EqualTo(segmentRect.width).Within(Epsilon));
            }

            var partialSegmentRect = _segmentedBarView.GetSegmentLocalRect(7);
            var partialFilledRect = _segmentedBarView.GetFilledSegmentLocalRect(7);
            Assert.That(partialFilledRect.width / partialSegmentRect.width, Is.EqualTo(0.4f).Within(Epsilon));

            var emptyFilledRect = _segmentedBarView.GetFilledSegmentLocalRect(8);
            Assert.That(emptyFilledRect.width, Is.EqualTo(0f).Within(Epsilon));
        }

        /// <summary>
        /// 発光量は範囲外入力でも 0〜1 に Clamp する。
        /// </summary>
        [Test]
        public void SetGlow_OutOfRangeValue_ClampsGlowIntensity()
        {
            CreateView();

            _segmentedBarView.SetGlow(2f);

            var glowIntensityField = typeof(SegmentedBarView).GetField("_glowIntensity", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(glowIntensityField!.GetValue(_segmentedBarView), Is.EqualTo(1f).Within(Epsilon));

            _segmentedBarView.SetGlow(-1f);

            Assert.That(glowIntensityField.GetValue(_segmentedBarView), Is.EqualTo(0f).Within(Epsilon));
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
            _gameObject = new GameObject("SegmentedBarView", typeof(RectTransform), typeof(CanvasRenderer));
            var rectTransform = _gameObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ViewWidth);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ViewHeight);
            _segmentedBarView = _gameObject.AddComponent<SegmentedBarView>();
        }
    }
}
