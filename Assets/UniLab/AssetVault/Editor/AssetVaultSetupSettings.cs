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
        public const string AssetPath = GeneratedAssetFolder.Path + "/AssetVaultSetupSettings.asset";

        [SerializeField] private DefaultAsset _rootFolder;

        /// <summary>
        /// AssetResource のルートパスを取得します。
        /// インスペクタで指定したフォルダ参照から解決し、未設定・非フォルダの場合は <see cref="DefaultRootPath"/> にフォールバックします。
        /// </summary>
        public string RootPath
        {
            get
            {
                if (_rootFolder == null)
                {
                    return DefaultRootPath;
                }

                // DefaultAsset はフォルダ以外（未知拡張子のファイル等）も代入可能なため、フォルダであることを検証する。
                var folderPath = AssetDatabase.GetAssetPath(_rootFolder);
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    return DefaultRootPath;
                }

                return folderPath;
            }
        }

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

            GeneratedAssetFolder.Ensure();

            settings = CreateInstance<AssetVaultSetupSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
