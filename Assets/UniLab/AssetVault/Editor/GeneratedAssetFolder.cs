using UnityEditor;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// 生成アセットの保存先フォルダ（Assets/Generated/UniLab）を保証する共有ヘルパーです。
    /// AssetVault が GetOrCreate するエディタ設定アセットの作成前に呼びます。
    /// </summary>
    internal static class GeneratedAssetFolder
    {
        /// <summary>生成アセットの保存先フォルダパスです。</summary>
        public const string Path = "Assets/Generated/UniLab";

        private const string GeneratedFolderPath = "Assets/Generated";
        private const string AssetsFolderPath = "Assets";
        private const string GeneratedFolderName = "Generated";
        private const string UniLabFolderName = "UniLab";

        /// <summary>
        /// <see cref="Path"/> が存在しない場合に作成します。
        /// </summary>
        public static void Ensure()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolderPath))
            {
                AssetDatabase.CreateFolder(AssetsFolderPath, GeneratedFolderName);
            }

            if (AssetDatabase.IsValidFolder(Path))
            {
                return;
            }

            AssetDatabase.CreateFolder(GeneratedFolderPath, UniLabFolderName);
        }
    }
}
