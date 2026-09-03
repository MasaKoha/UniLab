using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UniLab.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniLab.Tests.PlayMode.UI
{
    /// <summary>
    /// RadarChartView の値アニメーション完了を検証する。
    /// </summary>
    public class RadarChartViewAnimationTest
    {
        private const int MaximumWaitFrameCount = 120;

        private GameObject _gameObject;
        private RadarChartView _radarChartView;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("RadarChartViewAnimationTest", typeof(RectTransform), typeof(CanvasRenderer));
            _radarChartView = _gameObject.AddComponent<RadarChartView>();
            _radarChartView.Initialize(4);
            _radarChartView.SetValues(stackalloc float[] { 0.1f, 0.2f, 0.3f, 0.4f });
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
            }
        }

        /// <summary>
        /// AnimateTo 完了後は保持値が目標値と一致する。
        /// </summary>
        [UnityTest]
        public IEnumerator AnimateTo_AfterCompletion_MatchesTargetValues()
        {
            var targetValues = new[] { 0.8f, 0.5f, 0.2f, 1f };

            _radarChartView.AnimateTo(targetValues, 0.05f, RadarChartEasing.OutCubic);

            var waitedFrameCount = 0;
            while (_radarChartView.IsAnimating && waitedFrameCount < MaximumWaitFrameCount)
            {
                waitedFrameCount++;
                yield return null;
            }

            Assert.That(_radarChartView.IsAnimating, Is.False);
            var normalizedValues = GetNormalizedValues();

            for (var axisIndex = 0; axisIndex < targetValues.Length; axisIndex++)
            {
                Assert.That(normalizedValues[axisIndex], Is.EqualTo(targetValues[axisIndex]).Within(0.001f));
            }
        }

        private float[] GetNormalizedValues()
        {
            var normalizedValuesField = typeof(RadarChartView).GetField("_normalizedValues", BindingFlags.NonPublic | BindingFlags.Instance);
            return (float[])normalizedValuesField!.GetValue(_radarChartView);
        }
    }
}
