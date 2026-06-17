namespace UniLab.AssetVault
{
    /// <summary>
    /// <see cref="IAssetVaultCache"/> の現在の占有状況スナップショットです（ランタイム診断・デバッグ表示用）。
    /// </summary>
    public readonly struct AssetVaultCacheStats
    {
        public AssetVaultCacheStats(int entryCount, int referencedEntryCount, int pinnedEntryCount, int totalReferenceCount)
        {
            EntryCount = entryCount;
            ReferencedEntryCount = referencedEntryCount;
            PinnedEntryCount = pinnedEntryCount;
            TotalReferenceCount = totalReferenceCount;
        }

        /// <summary>cache が保持しているエントリ総数です。</summary>
        public int EntryCount { get; }

        /// <summary>参照カウントが 1 以上（使用中）のエントリ数です。</summary>
        public int ReferencedEntryCount { get; }

        /// <summary>Prewarm で pin 中のエントリ数です。</summary>
        public int PinnedEntryCount { get; }

        /// <summary>全エントリの参照カウント合計です。</summary>
        public int TotalReferenceCount { get; }

        /// <summary>参照カウント 0（TTL/LRU の解放待ち）のエントリ数です。</summary>
        public int UnreferencedEntryCount => EntryCount - ReferencedEntryCount;
    }
}
