using UnityEditor;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// Addressables player content build 用の MenuItem ショートカットです。
    /// 実体は <see cref="AssetVaultEditorOperations"/> に委譲する薄いラッパです。
    /// </summary>
    public static class AssetVaultBuildMenu
    {
        private const string BaseMenuPath = "UniLab/AssetVault/Build/";
        private const string NewBuildMenuPath = BaseMenuPath + "New Build";
        private const string ContentUpdateMenuPath = BaseMenuPath + "Update a Previous Build (Diff)";

        /// <summary>
        /// 新しい Addressables player content をビルドします。
        /// </summary>
        [MenuItem(NewBuildMenuPath)]
        public static void BuildNew()
        {
            AssetVaultEditorOperations.BuildNew();
        }

        /// <summary>
        /// 前回の content state file から Addressables content update をビルドします。
        /// </summary>
        [MenuItem(ContentUpdateMenuPath)]
        public static void BuildContentUpdate()
        {
            AssetVaultEditorOperations.BuildContentUpdate();
        }
    }
}
