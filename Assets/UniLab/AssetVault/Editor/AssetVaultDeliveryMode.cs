namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// 同期ルールの配信先です。Addressables のビルド/ロードパスの割り当てを決めます。
    /// </summary>
    public enum AssetVaultDeliveryMode
    {
        /// <summary>プレイヤービルドに同梱します（LocalBuildPath / LocalLoadPath）。</summary>
        Local,

        /// <summary>CDN 配信します（RemoteBuildPath / RemoteLoadPath）。</summary>
        Remote,
    }
}
