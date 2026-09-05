#if UNITY_EDITOR
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>実時間待機なしで省電力ポーリングの境界を検証します。</summary>
    public sealed class AiMailboxServerPollingTest
    {
        /// <summary>未処理時間が閾値に達した時点で待機間隔へ切り替えます。</summary>
        [TestCase(10f, 10f, 0.05f)]
        [TestCase(10f, 14.99f, 0.05f)]
        [TestCase(10f, 15f, 0.25f)]
        [TestCase(10f, 30f, 0.25f)]
        [TestCase(30f, 30f, 0.05f)]
        public void ResolvePollIntervalSwitchesAtIdleThreshold(float lastHandledAt, float now, float expectedInterval)
        {
            Assert.That(AiMailboxServer.ResolvePollInterval(lastHandledAt, now), Is.EqualTo(expectedInterval));
        }

        /// <summary>設定値を反映し、待機時に通常より短い間隔へ逆転しません。</summary>
        [TestCase(0.4f, 0.1f, 0.4f)]
        [TestCase(0.1f, 0.4f, 0.4f)]
        public void ConfiguredIntervalsNeverAccelerateWhileIdle(float activeInterval, float idleInterval, float expectedInterval)
        {
            const float IdleAfterSeconds = 2f;
            Assert.That(AiMailboxServer.ResolvePollInterval(0f, IdleAfterSeconds, IdleAfterSeconds, idleInterval, activeInterval), Is.EqualTo(expectedInterval));
        }
    }
}
#endif
