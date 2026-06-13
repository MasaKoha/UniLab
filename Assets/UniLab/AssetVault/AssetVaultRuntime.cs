namespace UniLab.AssetVault
{
    /// <summary>
    /// Addressables の RemoteLoadPath に埋めた実行時トークン {UniLab.AssetVault.AssetVaultRuntime.Environment} / {UniLab.AssetVault.AssetVaultRuntime.ContentPath} が参照する静的状態です。IAssetVaultService.InitializeAsync の前にアプリ層がセットします。
    /// </summary>
    public static class AssetVaultRuntime
    {
        /// <summary>
        /// アプリ層が IAssetVaultService.InitializeAsync の前にセットし、RemoteLoadPath の環境セグメントを切り替えるために使われます。
        /// </summary>
        public static string Environment { get; set; } = "prod";

        /// <summary>
        /// アプリ層が IContentVersionResolver の解決結果またはデバッグ上書きから IAssetVaultService.InitializeAsync の前にセットし、RemoteLoadPath のコンテンツ版パスセグメントに使われます。
        /// </summary>
        public static string ContentPath { get; set; } = null;
    }
}
