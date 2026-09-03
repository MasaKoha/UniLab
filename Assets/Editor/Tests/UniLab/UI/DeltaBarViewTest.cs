using NUnit.Framework;
using UniLab.UI;
using UnityEngine;

namespace UniLab.Tests.EditMode.UI
{
    /// <summary>
    /// DeltaBarView の座標計算と値 Clamp を検証する EditMode テスト。
    /// </summary>
    public class DeltaBarViewTest
    {
        private const float ViewWidth = 200f;
        private const float ViewHeight = 20f;
        private const float Epsilon = 0.001f;

        private GameObject _gameObject;
        private DeltaBarView _deltaBarView;

        /// <summary>
        /// 基準線中央で 0.5 を渡すと右半分の中央まで先端が伸びる。
        /// </summary>
        [Test]
        public void GetBarEndLocalPosition_PositiveValue_ReturnsExpectedPosition()
        {
            CreateView();
            _deltaBarView.Initialize();
            _deltaBarView.SetValue(0.5f);

            var barEndPosition = _deltaBarView.GetBarEndLocalPosition();

            Assert.That(barEndPosition.x, Is.EqualTo(50f).Within(Epsilon));
            Assert.That(barEndPosition.y, Is.EqualTo(0f).Within(Epsilon));
        }

        /// <summary>
        /// 範囲外入力でも先端位置は -1〜+1 の範囲に収まる。
        /// </summary>
        [Test]
        public void SetValue_OutOfRangeValue_ClampsBarEndPosition()
        {
            CreateView();
            _deltaBarView.Initialize();

            _deltaBarView.SetValue(2f);
            Assert.That(_deltaBarView.GetBarEndLocalPosition().x, Is.EqualTo(100f).Within(Epsilon));

            _deltaBarView.SetValue(-2f);
            Assert.That(_deltaBarView.GetBarEndLocalPosition().x, Is.EqualTo(-100f).Within(Epsilon));
        }

        /// <summary>
        /// 初期化前に値を渡しても暗黙初期化で例外を避ける。
        /// </summary>
        [Test]
        public void SetValue_BeforeInitialize_DoesNotThrow()
        {
            CreateView();

            Assert.That(() => _deltaBarView.SetValue(-0.25f), Throws.Nothing);

            var barEndPosition = _deltaBarView.GetBarEndLocalPosition();
            Assert.That(barEndPosition.x, Is.EqualTo(-25f).Within(Epsilon));
        }

        /// <summary>
        /// 基準線位置を変更すると先端計算も同じ位置基準に追従する。
        /// </summary>
        [Test]
        public void GetBarEndLocalPosition_WithCustomBaselinePosition_ReturnsExpectedPosition()
        {
            CreateView();
            _deltaBarView.Initialize();
            _deltaBarView.SetStyle(new DeltaBarStyle(
                positiveColor: Color.green,
                negativeColor: Color.red,
                zeroColor: Color.white,
                backgroundColor: Color.black,
                baselineColor: Color.white,
                baselineThickness: 2f,
                baselinePosition: 0.25f,
                outlineColor: Color.clear,
                outlineThickness: 0f));
            _deltaBarView.SetValue(-0.5f);

            var barEndPosition = _deltaBarView.GetBarEndLocalPosition();

            Assert.That(barEndPosition.x, Is.EqualTo(-25f).Within(Epsilon));
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
            _gameObject = new GameObject("DeltaBarView", typeof(RectTransform), typeof(CanvasRenderer));
            var rectTransform = _gameObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ViewWidth);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ViewHeight);
            _deltaBarView = _gameObject.AddComponent<DeltaBarView>();
        }
    }
}
