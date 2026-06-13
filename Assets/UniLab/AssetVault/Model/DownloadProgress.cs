namespace UniLab.AssetVault
{
    /// <summary>
    /// 頻繁に更新される UI 購読者向けに、依存関係のダウンロード進捗を allocation に配慮した値型として通知します。
    /// </summary>
    // perf: ダウンロード中に高頻度で発行されるため、struct で tick ごとのヒープ確保を避ける。
    public readonly struct DownloadProgress
    {
        /// <summary>
        /// 実行中の依存関係ダウンロードで、すでにダウンロード済みのバイト数を取得します。
        /// </summary>
        public long DownloadedBytes { get; }

        /// <summary>
        /// 実行中の依存関係ダウンロードで想定される総バイト数を取得します。
        /// </summary>
        public long TotalBytes { get; }

        /// <summary>
        /// progress UI が使用する、正規化されたダウンロード完了率を取得します。
        /// </summary>
        public float Ratio { get; }

        /// <summary>
        /// ダウンロード実行中に vault service が通知する進捗情報を作成します。
        /// </summary>
        public DownloadProgress(long downloadedBytes, long totalBytes, float ratio)
        {
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            Ratio = ratio;
        }
    }
}
