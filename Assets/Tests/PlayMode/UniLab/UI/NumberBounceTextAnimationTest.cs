using System.Collections;
using NUnit.Framework;
using TMPro;
using UniLab.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniLab.Tests.PlayMode.UI
{
    /// <summary>
    /// NumberBounceText の演出完了を検証する PlayMode テスト。
    /// </summary>
    public class NumberBounceTextAnimationTest
    {
        private const int MaximumWaitFrameCount = 180;
        private const float Epsilon = 0.001f;

        private GameObject _gameObject;
        private NumberBounceText _numberBounceText;
        private TextMeshProUGUI _textMeshPro;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("NumberBounceTextAnimationTest", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            _numberBounceText = _gameObject.AddComponent<NumberBounceText>();
            _textMeshPro = _gameObject.GetComponent<TextMeshProUGUI>();
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
        /// Bounce は完了後に等倍へ戻り、再生中フラグを下ろす。
        /// </summary>
        [UnityTest]
        public IEnumerator Bounce_AfterCompletion_ReturnsScaleToOne()
        {
            _numberBounceText.Bounce(42, 0.05f, 1.6f);

            var waitedFrameCount = 0;
            while (_numberBounceText.IsPlaying && waitedFrameCount < MaximumWaitFrameCount)
            {
                waitedFrameCount++;
                yield return null;
            }

            Assert.That(_numberBounceText.IsPlaying, Is.False);
            Assert.That(_numberBounceText.transform.localScale.x, Is.EqualTo(1f).Within(Epsilon));
        }

        /// <summary>
        /// CountUp は最終値に到達し、最後の跳ね演出まで終えて停止する。
        /// </summary>
        [UnityTest]
        public IEnumerator CountUp_AfterCompletion_StopsAtTargetValue()
        {
            _numberBounceText.Format = "Lv{0}";
            _numberBounceText.CountUp(3, 7, 0.05f);

            var waitedFrameCount = 0;
            while (_numberBounceText.IsPlaying && waitedFrameCount < MaximumWaitFrameCount)
            {
                waitedFrameCount++;
                yield return null;
            }

            Assert.That(_numberBounceText.IsPlaying, Is.False);
            Assert.That(_textMeshPro.text, Is.EqualTo("Lv7"));
        }
    }
}
