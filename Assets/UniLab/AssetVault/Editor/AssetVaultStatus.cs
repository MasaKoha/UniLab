namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault の Addressables 構成の現状（ダッシュボード表示用の読み取り専用スナップショット）です。
    /// </summary>
    public readonly struct AssetVaultStatus
    {
        public AssetVaultStatus(
            bool settingsInitialized,
            string remoteLoadPath,
            int localGroupCount,
            int remoteGroupCount,
            string rootPath,
            bool internalFolderExists,
            bool externalFolderExists)
        {
            SettingsInitialized = settingsInitialized;
            RemoteLoadPath = remoteLoadPath;
            LocalGroupCount = localGroupCount;
            RemoteGroupCount = remoteGroupCount;
            RootPath = rootPath;
            InternalFolderExists = internalFolderExists;
            ExternalFolderExists = externalFolderExists;
        }

        /// <summary>Addressables settings が初期化済みかどうかです。</summary>
        public bool SettingsInitialized { get; }

        /// <summary>有効 profile の RemoteLoadPath 値です。未初期化時は空文字です。</summary>
        public string RemoteLoadPath { get; }

        /// <summary>Local_ プレフィックスの Addressables グループ数です。</summary>
        public int LocalGroupCount { get; }

        /// <summary>Remote_ プレフィックスの Addressables グループ数です。</summary>
        public int RemoteGroupCount { get; }

        /// <summary>AssetResource のルートパスです。</summary>
        public string RootPath { get; }

        /// <summary>AssetResource/Internal フォルダが存在するかどうかです。</summary>
        public bool InternalFolderExists { get; }

        /// <summary>AssetResource/External フォルダが存在するかどうかです。</summary>
        public bool ExternalFolderExists { get; }
    }
}
