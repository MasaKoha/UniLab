#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AssetVault
{
    /// <summary>
    /// 実行中の <see cref="IAssetVaultCache"/> を自己登録させ、Editor のデバッグ表示から参照できるようにする静的レジストリです。
    /// Runtime asmdef に置くことで Editor/Debug を参照せずに済ませています。デバッグ専用のためリリースビルドには含めません。
    /// </summary>
    public static class AssetVaultCacheStatsRegistry
    {
        // 複数生成は想定しないため、最後に登録された cache のみを保持する。
        private static IAssetVaultCache _cache;

        /// <summary>現在 cache が登録されているかどうかを返します。</summary>
        public static bool HasCache => _cache != null;

        /// <summary>
        /// cache を現在アクティブなものとして登録します。<see cref="AssetVaultCache"/> のコンストラクタから呼ばれます。
        /// </summary>
        public static void Register(IAssetVaultCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// cache の登録を解除します。保持中のものと同一の場合のみクリアします（後発の cache を誤って消さないため）。
        /// <see cref="AssetVaultCache.Dispose"/> から呼ばれます。
        /// </summary>
        public static void Unregister(IAssetVaultCache cache)
        {
            if (ReferenceEquals(_cache, cache))
            {
                _cache = null;
            }
        }

        /// <summary>
        /// 登録中の cache から統計スナップショットを取得します。未登録の場合は false を返します。
        /// </summary>
        public static bool TryGetStats(out AssetVaultCacheStats stats)
        {
            if (_cache == null)
            {
                stats = default;
                return false;
            }

            stats = _cache.GetStats();
            return true;
        }
    }
}
#endif
