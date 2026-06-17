namespace UniLab.AssetVault
{
    /// <summary>
    /// EnsureDownloadedAsync の事前ダウンロードフロー（サイズ確認 → ユーザー確認 → リトライ付き DL）の最終結果を表します。
    /// </summary>
    public enum AssetVaultDownloadResult
    {
        /// <summary>
        /// 既定値（未設定）。default(AssetVaultDownloadResult) がこの値になります。
        /// </summary>
        None = 0,

        /// <summary>
        /// 未取得分のサイズが 0 以下で、ダウンロードする対象が無かったことを表します。
        /// </summary>
        NothingToDownload,

        /// <summary>
        /// 依存関係のダウンロードが正常に完了したことを表します。
        /// </summary>
        Completed,

        /// <summary>
        /// 確認ダイアログでユーザーがダウンロードを拒否したことを表します。
        /// </summary>
        CanceledByUser,

        /// <summary>
        /// リトライを尽くしてもダウンロードに失敗したことを表します。
        /// </summary>
        Failed
    }
}
