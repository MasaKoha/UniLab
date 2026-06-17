using System.Collections.Generic;
using System.Linq;

namespace UniLab.AssetVault
{
    /// <summary>
    /// <see cref="AssetVaultCache"/> の遅延解放（TTL 期限切れ・LRU 上限超過）の「どれを解放するか」の判定ロジックです。
    /// Addressables ハンドルに依存しない純関数として切り出し、境界条件（TTL ちょうど・LRU の取りすぎ）を EditMode で単体テストできるようにしています。
    /// </summary>
    internal static class AssetVaultCacheEvictionPolicy
    {
        /// <summary>
        /// エントリが TTL 期限切れ（未参照かつ最終解放から TTL 以上経過）かどうかを判定します。
        /// TtlSeconds &lt;= 0（即時解放）の扱いは呼び出し側の責務で、本メソッドは TTL &gt; 0 前提の経過時間判定だけを行います。
        /// </summary>
        internal static bool IsExpired(int refCount, float lastReleaseTime, float now, float ttlSeconds)
        {
            return refCount <= 0 && now - lastReleaseTime >= ttlSeconds;
        }

        /// <summary>
        /// LRU 上限超過分として解放すべきキーを「未参照のうち最終解放時刻が古い順」に選びます。
        /// capacity &lt;= 0 は無制限（空を返す）。参照中エントリは解放対象にしません。未参照が足りなければ取れる分だけ返します。
        /// </summary>
        internal static List<TKey> SelectOverCapacityKeys<TKey>(
            IReadOnlyList<(TKey key, int refCount, float lastReleaseTime)> entries,
            int capacity)
        {
            var keysToReclaim = new List<TKey>();
            if (capacity <= 0 || entries.Count <= capacity)
            {
                return keysToReclaim;
            }

            var unreferencedOldestFirst = entries
                .Where(entry => entry.refCount <= 0)
                .OrderBy(entry => entry.lastReleaseTime)
                .ToList();

            var removableCount = entries.Count - capacity;
            for (var index = 0; index < removableCount && index < unreferencedOldestFirst.Count; index++)
            {
                keysToReclaim.Add(unreferencedOldestFirst[index].key);
            }

            return keysToReclaim;
        }
    }
}
