using System.Collections;
using NUnit.Framework;
using UniLab.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniLab.Tests.PlayMode.UI
{
    /// <summary>
    /// SegmentedBarView と DeltaBarView の値アニメーション完了を検証する。
    /// </summary>
    public class ChartAnimationTest
    {
        private const int MaximumWaitFrameCount = 120;
        private const float Epsilon = 0.001f;

        private GameObject _segmentedBarGameObject;
        private SegmentedBarView _segmentedBarView;
        private GameObject _deltaBarGameObject;
        private DeltaBarView _deltaBarView;

        [SetUp]
        public void SetUp()
        {
            _segmentedBarGameObject = new GameObject("SegmentedBarAnimationTest", typeof(RectTransform), typeof(CanvasRenderer));
            var segmentedRectTransform = _segmentedBarGameObject.GetComponent<RectTransform>();
            segmentedRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 200f);
            segmentedRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20f);
            _segmentedBarView = _segmentedBarGameObject.AddComponent<SegmentedBarView>();
            _segmentedBarView.Initialize(10);
            _segmentedBarView.SetValue(0.2f);

            _deltaBarGameObject = new GameObject("DeltaBarAnimationTest", typeof(RectTransform), typeof(CanvasRenderer));
            var deltaRectTransform = _deltaBarGameObject.GetComponent<RectTransform>();
            deltaRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 200f);
            deltaRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20f);
            _deltaBarView = _deltaBarGameObject.AddComponent<DeltaBarView>();
            _deltaBarView.Initialize();
            _deltaBarView.SetValue(-0.25f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_segmentedBarGameObject != null)
            {
                Object.Destroy(_segmentedBarGameObject);
            }

            if (_deltaBarGameObject != null)
            {
                Object.Destroy(_deltaBarGameObject);
            }
        }

        /// <summary>
        /// SegmentedBarView の AnimateTo は再生中フラグを立て、完了後に目標値へ到達する。
        /// </summary>
        [UnityTest]
        public IEnumerator SegmentedBarAnimateTo_AfterCompletion_ReachesTargetValue()
        {
            _segmentedBarView.AnimateTo(0.74f, 0.05f, RadarChartEasing.OutCubic);

            Assert.That(_segmentedBarView.IsAnimating, Is.True);

            var waitedFrameCount = 0;
            while (_segmentedBarView.IsAnimating && waitedFrameCount < MaximumWaitFrameCount)
            {
                waitedFrameCount++;
                yield return null;
            }

            Assert.That(_segmentedBarView.IsAnimating, Is.False);
            var segmentRect = _segmentedBarView.GetSegmentLocalRect(7);
            var filledRect = _segmentedBarView.GetFilledSegmentLocalRect(7);
            Assert.That(filledRect.width / segmentRect.width, Is.EqualTo(0.4f).Within(Epsilon));
        }

        /// <summary>
        /// DeltaBarView の AnimateTo は再生中フラグを立て、完了後に目標位置へ到達する。
        /// </summary>
        [UnityTest]
        public IEnumerator DeltaBarAnimateTo_AfterCompletion_ReachesTargetValue()
        {
            _deltaBarView.AnimateTo(0.8f, 0.05f, RadarChartEasing.OutCubic);

            Assert.That(_deltaBarView.IsAnimating, Is.True);

            var waitedFrameCount = 0;
            while (_deltaBarView.IsAnimating && waitedFrameCount < MaximumWaitFrameCount)
            {
                waitedFrameCount++;
                yield return null;
            }

            Assert.That(_deltaBarView.IsAnimating, Is.False);
            Assert.That(_deltaBarView.GetBarEndLocalPosition().x, Is.EqualTo(80f).Within(Epsilon));
        }
    }
}
