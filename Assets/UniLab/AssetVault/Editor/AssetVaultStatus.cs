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
            int syncRuleCount,
            int validFolderCount)
        {
            SettingsInitialized = settingsInitialized;
            RemoteLoadPath = remoteLoadPath;
            LocalGroupCount = localGroupCount;
            RemoteGroupCount = remoteGroupCount;
            SyncRuleCount = syncRuleCount;
            ValidFolderCount = validFolderCount;
        }

        /// <summary>Addressables settings が初期化済みかどうかです。</summary>
        public bool SettingsInitialized { get; }

        /// <summary>有効 profile の RemoteLoadPath 値です。未初期化時は空文字です。</summary>
        public string RemoteLoadPath { get; }

        /// <summary>Local_ プレフィックスの Addressables グループ数です。</summary>
        public int LocalGroupCount { get; }

        /// <summary>Remote_ プレフィックスの Addressables グループ数です。</summary>
        public int RemoteGroupCount { get; }

        /// <summary>登録済みの同期ルール数です。</summary>
        public int SyncRuleCount { get; }

        /// <summary>対象フォルダが実在する（解決できる）同期ルール数です。</summary>
        public int ValidFolderCount { get; }
    }
}
