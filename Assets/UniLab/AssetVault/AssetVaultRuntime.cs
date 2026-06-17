namespace UniLab.AssetVault
{
    /// <summary>
    /// Addressables の RemoteLoadPath に埋めた実行時トークン {UniLab.AssetVault.AssetVaultRuntime.BaseUrl} / {UniLab.AssetVault.AssetVaultRuntime.ContentPath} が参照する静的状態です。IAssetVaultService.InitializeAsync の前にアプリ層がセットします。
    /// </summary>
    public static class AssetVaultRuntime
    {
        /// <summary>
        /// 環境ごとのホスト込み基底 URL です（例 https://dev1.xxx.xxx/app）。env から URL へのマッピングはアプリ config が持ちます。
        /// </summary>
        public static string BaseUrl { get; set; }

        /// <summary>
        /// version.json の path です。RemoteLoadPath の不透明な版セグメントに使われます。
        /// </summary>
        public static string ContentPath { get; set; }
    }
}
