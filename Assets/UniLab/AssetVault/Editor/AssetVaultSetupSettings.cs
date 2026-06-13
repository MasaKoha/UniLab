using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault の Addressables 自動構成に使う設定を保持します。
    /// </summary>
    public sealed class AssetVaultSetupSettings : ScriptableObject
    {
        /// <summary>
        /// AssetResource の既定ルートパスです。
        /// </summary>
        public const string DefaultRootPath = "Assets/AssetResource";

        /// <summary>
        /// 設定アセットの保存先パスです。
        /// </summary>
        public const string AssetPath = "Assets/Generated/UniLab/AssetVaultSetupSettings.asset";

        private const string GeneratedFolderPath = "Assets/Generated";
        private const string UniLabGeneratedFolderPath = "Assets/Generated/UniLab";
        private const string AssetsFolderPath = "Assets";
        private const string GeneratedFolderName = "Generated";
        private const string UniLabFolderName = "UniLab";

        [SerializeField] private string _rootPath = DefaultRootPath;

        /// <summary>
        /// AssetResource のルートパスを取得します。
        /// </summary>
        public string RootPath => _rootPath;

        /// <summary>
        /// 設定アセットを取得し、存在しない場合は作成します。
        /// </summary>
        public static AssetVaultSetupSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AssetVaultSetupSettings>(AssetPath);
            if (settings != null)
            {
                return settings;
            }

            EnsureFolder();

            settings = CreateInstance<AssetVaultSetupSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolderPath))
            {
                AssetDatabase.CreateFolder(AssetsFolderPath, GeneratedFolderName);
            }

            if (AssetDatabase.IsValidFolder(UniLabGeneratedFolderPath))
            {
                return;
            }

            AssetDatabase.CreateFolder(GeneratedFolderPath, UniLabFolderName);
        }
    }
}
