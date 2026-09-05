#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
namespace UniLab.AI.Tests
{
    /// <summary>PR4 の観測契約をシーンなしで検証します。</summary>
    public sealed class AiCaptureSupportTest
    {
        private const byte MaximumChannel = byte.MaxValue;
        private const double HalfChannelRange = MaximumChannel / 2.0;
        private const double DeviationTolerance = 0.000001;

        /// <summary>単色の画像は明度によらず標準偏差ゼロです。</summary>
        [TestCase((byte)0)]
        [TestCase((byte)120)]
        [TestCase(MaximumChannel)]
        public void SolidColorHasZeroDeviation(byte channel)
        {
            var color = new Color32(channel, channel, channel, MaximumChannel);
            Assert.That(AiCaptureSupport.ComputeLuminanceDeviation(new[] { color, color }), Is.Zero);
        }

        /// <summary>白黒半々の母標準偏差は輝度範囲の半分です。</summary>
        [Test]
        public void HalfBlackHalfWhiteHasLargeDeviation()
        {
            var pixels = new[]
            {
                new Color32(0, 0, 0, MaximumChannel),
                new Color32(MaximumChannel, MaximumChannel, MaximumChannel, MaximumChannel),
            };
            Assert.That(AiCaptureSupport.ComputeLuminanceDeviation(pixels), Is.EqualTo(HalfChannelRange).Within(DeviationTolerance));
        }
    }
}
#endif
