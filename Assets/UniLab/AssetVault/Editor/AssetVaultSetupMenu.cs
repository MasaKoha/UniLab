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
        private const string OpenSetupSettingsMenuPath = BaseMenuPath + "Open Setup Settings";

        /// <summary>
        /// AssetVaultSetupSettings を選択して Inspector で開きます。Sync AssetResource はその Inspector で実行します。
        /// </summary>
        [MenuItem(OpenSetupSettingsMenuPath)]
        public static void OpenSetupSettings()
        {
            AssetVaultEditorOperations.OpenSetupSettings();
        }
    }
}
