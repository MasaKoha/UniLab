namespace UniLab.AssetVault
{
    /// <summary>
    /// <see cref="AssetVaultCache"/> の遅延解放ポリシーです。
    /// </summary>
    public readonly struct AssetVaultCacheSettings
    {
        /// <summary>既定設定（TTL=10秒・上限64件）。</summary>
        public static AssetVaultCacheSettings Default => new AssetVaultCacheSettings(10f, 64);

        /// <summary>
        /// 参照カウントが 0 になってからキャッシュに保持する秒数（TTL）。0 以下なら 0 で即解放します。
        /// </summary>
        public float TtlSeconds { get; }

        /// <summary>
        /// キャッシュに保持するエントリ数の上限（LRU）。0 以下なら無制限です。超過時は未参照の古いものから解放します。
        /// </summary>
        public int Capacity { get; }

        public AssetVaultCacheSettings(float ttlSeconds, int capacity)
        {
            TtlSeconds = ttlSeconds;
            Capacity = capacity;
        }
    }
}
