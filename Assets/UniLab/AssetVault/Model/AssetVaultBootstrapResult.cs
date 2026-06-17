namespace UniLab.AssetVault
{
    /// <summary>
    /// 起動シーケンス（初期化 → カタログ更新確認 → 初期必須アセットの事前ダウンロード）の最終結果です。
    /// アプリは Initialized と各結果を見て、ローディング解除・リトライ導線・致命エラー表示を出し分けます。
    /// </summary>
    public readonly struct AssetVaultBootstrapResult
    {
        /// <summary>
        /// 初期化結果と更新結果をまとめた起動結果を作成します。
        /// </summary>
        public AssetVaultBootstrapResult(bool initialized, AssetVaultUpdateResult updateResult)
        {
            Initialized = initialized;
            UpdateResult = updateResult;
        }

        /// <summary>InitializeAsync が（リトライ込みで）成功したかどうかです。false なら配信基盤が使えず、初期化のリトライが必要です。</summary>
        public bool Initialized { get; }

        /// <summary>初期化後のカタログ更新確認と初期事前ダウンロードの結果です。Initialized が false のときは既定値です。</summary>
        public AssetVaultUpdateResult UpdateResult { get; }

        /// <summary>
        /// 起動が完了し、ゲーム本編へ進んでよい状態かどうかを取得します（初期化成功かつ初期ダウンロードが失敗していない）。
        /// </summary>
        public bool IsReady => Initialized && UpdateResult.DownloadResult != AssetVaultDownloadResult.Failed;
    }
}
