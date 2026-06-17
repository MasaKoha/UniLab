using NUnit.Framework;
using UniLab.AssetVault;

namespace UniLab.Tests.EditMode.AssetVault
{
    /// <summary>
    /// リトライ間隔計算 <see cref="AssetVaultRetryPolicy"/> の単体テストです。
    /// Download / Update のリトライが共有する指数バックオフの倍増と上限キャップを固定します。
    /// </summary>
    public class AssetVaultRetryPolicyTest
    {
        /// <summary>
        /// attempt が増えるごとに 0.5s から倍々（1s, 2s, 4s）で伸びることを検証します。
        /// </summary>
        [Test]
        public void GetBackoffDelaySeconds_DoublesPerAttempt()
        {
            Assert.AreEqual(0.5f, AssetVaultRetryPolicy.GetBackoffDelaySeconds(0), 0.0001f);
            Assert.AreEqual(1f, AssetVaultRetryPolicy.GetBackoffDelaySeconds(1), 0.0001f);
            Assert.AreEqual(2f, AssetVaultRetryPolicy.GetBackoffDelaySeconds(2), 0.0001f);
            Assert.AreEqual(4f, AssetVaultRetryPolicy.GetBackoffDelaySeconds(3), 0.0001f);
        }

        /// <summary>
        /// 上限（MaxRetryDelaySeconds=8s）に達したらそれ以上伸びず頭打ちになることを検証します。
        /// </summary>
        [Test]
        public void GetBackoffDelaySeconds_CapsAtMax()
        {
            Assert.AreEqual(AssetVaultRetryPolicy.MaxRetryDelaySeconds, AssetVaultRetryPolicy.GetBackoffDelaySeconds(4), 0.0001f);
            Assert.AreEqual(AssetVaultRetryPolicy.MaxRetryDelaySeconds, AssetVaultRetryPolicy.GetBackoffDelaySeconds(5), 0.0001f);
            Assert.AreEqual(AssetVaultRetryPolicy.MaxRetryDelaySeconds, AssetVaultRetryPolicy.GetBackoffDelaySeconds(10), 0.0001f);
        }
    }
}
