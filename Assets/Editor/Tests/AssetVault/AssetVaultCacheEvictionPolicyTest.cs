using System.Collections.Generic;
using NUnit.Framework;
using UniLab.AssetVault;

namespace UniLab.Tests.EditMode.AssetVault
{
    /// <summary>
    /// キャッシュ退避判定の純ロジック <see cref="AssetVaultCacheEvictionPolicy"/> の単体テストです。
    /// TTL 境界（ちょうど期限）と LRU の選択順・取りすぎ防止という、バグを生みやすい箇所を固定します。
    /// </summary>
    public class AssetVaultCacheEvictionPolicyTest
    {
        /// <summary>
        /// 参照中（refCount &gt; 0）のエントリは、TTL を過ぎていても期限切れにしないことを検証します。
        /// </summary>
        [Test]
        public void IsExpired_Referenced_NeverExpires()
        {
            var expired = AssetVaultCacheEvictionPolicy.IsExpired(refCount: 1, lastReleaseTime: 0f, now: 100f, ttlSeconds: 10f);

            Assert.IsFalse(expired);
        }

        /// <summary>
        /// 未参照で最終解放からちょうど TTL 経過した境界では期限切れ（>= 判定）になることを検証します。
        /// </summary>
        [Test]
        public void IsExpired_UnreferencedExactlyAtTtl_IsExpired()
        {
            var expired = AssetVaultCacheEvictionPolicy.IsExpired(refCount: 0, lastReleaseTime: 0f, now: 10f, ttlSeconds: 10f);

            Assert.IsTrue(expired);
        }

        /// <summary>
        /// 未参照だが TTL 未満の経過では期限切れにしないことを検証します。
        /// </summary>
        [Test]
        public void IsExpired_UnreferencedBeforeTtl_IsNotExpired()
        {
            var expired = AssetVaultCacheEvictionPolicy.IsExpired(refCount: 0, lastReleaseTime: 0f, now: 9.9f, ttlSeconds: 10f);

            Assert.IsFalse(expired);
        }

        /// <summary>
        /// 上限以下なら何も解放しないことを検証します。
        /// </summary>
        [Test]
        public void SelectOverCapacityKeys_WithinCapacity_ReturnsEmpty()
        {
            var entries = new List<(string key, int refCount, float lastReleaseTime)>
            {
                ("a", 0, 1f),
                ("b", 0, 2f),
            };

            var keysToReclaim = AssetVaultCacheEvictionPolicy.SelectOverCapacityKeys(entries, capacity: 2);

            Assert.IsEmpty(keysToReclaim);
        }

        /// <summary>
        /// capacity &lt;= 0（無制限）なら何も解放しないことを検証します。
        /// </summary>
        [Test]
        public void SelectOverCapacityKeys_UnlimitedCapacity_ReturnsEmpty()
        {
            var entries = new List<(string key, int refCount, float lastReleaseTime)>
            {
                ("a", 0, 1f),
                ("b", 0, 2f),
                ("c", 0, 3f),
            };

            var keysToReclaim = AssetVaultCacheEvictionPolicy.SelectOverCapacityKeys(entries, capacity: 0);

            Assert.IsEmpty(keysToReclaim);
        }

        /// <summary>
        /// 上限超過時、未参照を「最終解放が古い順」に超過分だけ解放し、参照中は対象外であることを検証します。
        /// </summary>
        [Test]
        public void SelectOverCapacityKeys_OverCapacity_ReclaimsOldestUnreferencedFirst()
        {
            var entries = new List<(string key, int refCount, float lastReleaseTime)>
            {
                ("a", 0, 1f),
                ("b", 0, 3f),
                ("referenced", 1, 0f),
                ("d", 0, 2f),
            };

            // 4件 → 上限2 なので2件解放。未参照を古い順に並べると a(1) < d(2) < b(3)。参照中 'referenced' は対象外。
            var keysToReclaim = AssetVaultCacheEvictionPolicy.SelectOverCapacityKeys(entries, capacity: 2);

            Assert.AreEqual(2, keysToReclaim.Count);
            Assert.AreEqual("a", keysToReclaim[0]);
            Assert.AreEqual("d", keysToReclaim[1]);
            CollectionAssert.DoesNotContain(keysToReclaim, "referenced");
        }

        /// <summary>
        /// 未参照が超過分に足りない場合は、取れる分だけ解放する（参照中は触らない）ことを検証します。
        /// </summary>
        [Test]
        public void SelectOverCapacityKeys_NotEnoughUnreferenced_ReclaimsAvailableOnly()
        {
            var entries = new List<(string key, int refCount, float lastReleaseTime)>
            {
                ("a", 1, 0f),
                ("b", 1, 0f),
                ("c", 0, 5f),
            };

            // 3件 → 上限1 なので2件解放したいが、未参照は c のみ。c だけ返す。
            var keysToReclaim = AssetVaultCacheEvictionPolicy.SelectOverCapacityKeys(entries, capacity: 1);

            Assert.AreEqual(1, keysToReclaim.Count);
            Assert.AreEqual("c", keysToReclaim[0]);
        }
    }
}
