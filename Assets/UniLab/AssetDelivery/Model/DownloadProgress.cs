namespace UniLab.AssetDelivery
{
    /// <summary>
    /// Reports dependency download progress to UI subscribers as an allocation-conscious value type for frequent updates.
    /// </summary>
    // perf: emitted frequently during downloads; struct avoids per-tick heap allocation.
    public readonly record struct DownloadProgress
    {
        /// <summary>
        /// Gets the number of bytes that have already been downloaded for the active dependency download.
        /// </summary>
        public long DownloadedBytes { get; }

        /// <summary>
        /// Gets the total number of bytes expected for the active dependency download.
        /// </summary>
        public long TotalBytes { get; }

        /// <summary>
        /// Gets the normalized download completion ratio used by progress UI.
        /// </summary>
        public float Ratio { get; }

        /// <summary>
        /// Creates progress information emitted by the delivery service while a download is running.
        /// </summary>
        public DownloadProgress(long downloadedBytes, long totalBytes, float ratio)
        {
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            Ratio = ratio;
        }
    }
}
