using UnityEditor;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetResource のフォルダ規約から Addressables を自動構成する MenuItem ショートカットです。
    /// 実体は <see cref="AssetVaultEditorOperations"/> に委譲する薄いラッパです。
    /// </summary>
    public static class AssetVaultSetupMenu
    {
        private const string BaseMenuPath = "UniLab/AssetVault/Setup/";
        private const string SyncAssetResourceMenuPath = BaseMenuPath + "Sync AssetResource";
        private const string OpenSetupSettingsMenuPath = BaseMenuPath + "Open Setup Settings";

        /// <summary>
        /// AssetResource の Internal / External フォルダ規約を Addressables に同期します。
        /// </summary>
        [MenuItem(SyncAssetResourceMenuPath)]
        public static void SyncAssetResource()
        {
            AssetVaultEditorOperations.SyncAssetResource();
        }

        /// <summary>
        /// AssetVaultSetupSettings を選択して Inspector で開きます。
        /// </summary>
        [MenuItem(OpenSetupSettingsMenuPath)]
        public static void OpenSetupSettings()
        {
            AssetVaultEditorOperations.OpenSetupSettings();
        }
    }
}
