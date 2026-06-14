using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault の Addressables 自動構成に使う設定を保持します。
    /// 同梱(Local)ルートフォルダ（必須）と CDN(Remote)ルートフォルダ（任意）の2つを指定します。
    /// </summary>
    public sealed class AssetVaultSetupSettings : ScriptableObject
    {
        /// <summary>
        /// 設定アセットの保存先パスです。
        /// </summary>
        public const string AssetPath = GeneratedAssetFolder.Path + "/AssetVaultSetupSettings.asset";

        [Tooltip("同梱(Local)アセットのルートフォルダ。【必須】直下サブフォルダがグループ Local_<名> になります。")]
        [SerializeField] private DefaultAsset _localFolder;

        [Tooltip("CDN(Remote)アセットのルートフォルダ。【任意】未設定可。直下サブフォルダがグループ Remote_<名> になります。")]
        [SerializeField] private DefaultAsset _remoteFolder;

        /// <summary>
        /// 同梱(Local)ルートフォルダのアセットパスです。未設定・非フォルダの場合は null。必須項目です。
        /// </summary>
        public string LocalFolderPath => ResolveFolderPath(_localFolder);

        /// <summary>
        /// CDN(Remote)ルートフォルダのアセットパスです。未設定・非フォルダの場合は null（任意項目）。
        /// </summary>
        public string RemoteFolderPath => ResolveFolderPath(_remoteFolder);

        /// <summary>
        /// 設定アセットを副作用なく読み込みます（存在しなければ false）。読み取り専用 UI から使い、アセットの自動生成を避けます。
        /// </summary>
        public static bool TryLoad(out AssetVaultSetupSettings settings)
        {
            settings = AssetDatabase.LoadAssetAtPath<AssetVaultSetupSettings>(AssetPath);
            return settings != null;
        }

        /// <summary>
        /// 設定アセットを取得し、存在しない場合は作成します。フォルダはユーザーが Inspector で指定します。
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

        // DefaultAsset はフォルダ以外（未知拡張子のファイル等）も代入可能なため、フォルダであることを検証する。
        private static string ResolveFolderPath(DefaultAsset folder)
        {
            if (folder == null)
            {
                return null;
            }

            var folderPath = AssetDatabase.GetAssetPath(folder);
            return AssetDatabase.IsValidFolder(folderPath) ? folderPath : null;
        }
    }
}
