namespace UniLab.AssetVault
{
    /// <summary>
    /// asset vault の準備中またはダウンロード中に、アプリケーションのロード UI が監視する runtime 状態を表します。
    /// </summary>
    public enum AssetVaultState
    {
        /// <summary>
        /// 既定値（未設定）。default(AssetVaultState) がこの値になります。
        /// </summary>
        None = 0,

        /// <summary>
        /// サービスは起動シーケンスによってまだ初期化されていません。
        /// </summary>
        NotInitialized,

        /// <summary>
        /// 起動シーケンスが配信システムを初期化し、catalog データをロードしています。
        /// </summary>
        Initializing,

        /// <summary>
        /// 配信システムは更新確認、ダウンロード、scoped asset loading を実行できます。
        /// </summary>
        Ready,

        /// <summary>
        /// サービスはアプリケーションから要求された依存関係をダウンロードしています。
        /// </summary>
        Downloading,

        /// <summary>
        /// 直近の初期化またはダウンロード操作が失敗し、アプリケーションは初期化を再試行できます。
        /// </summary>
        Failed
    }
}
