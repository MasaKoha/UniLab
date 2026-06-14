using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault の Addressables 自動構成に使う設定を保持します。
    /// 同期対象は固定規約ではなく <see cref="AssetVaultSyncRule"/> のリストで定義し、プロジェクトごとのフォルダ構成へ対応します。
    /// </summary>
    public sealed class AssetVaultSetupSettings : ScriptableObject
    {
        /// <summary>
        /// 設定アセットの保存先パスです。
        /// </summary>
        public const string AssetPath = GeneratedAssetFolder.Path + "/AssetVaultSetupSettings.asset";

        // 既定フォルダ規約。新規作成時に存在すれば利便性のためルールへシードする（Addressables の Local/Remote に合わせた命名）。
        private const string DefaultLocalFolderPath = "Assets/AssetResource/Local";
        private const string DefaultRemoteFolderPath = "Assets/AssetResource/Remote";

        [SerializeField] private List<AssetVaultSyncRule> _syncRules = new List<AssetVaultSyncRule>();

        /// <summary>
        /// 同期ルール一覧です。各ルールが対象フォルダと配信先(Local/Remote)を持ちます。
        /// </summary>
        public IReadOnlyList<AssetVaultSyncRule> SyncRules => _syncRules;

        /// <summary>
        /// 設定アセットを取得し、存在しない場合は作成します。新規時は既定フォルダがあれば初期ルールをシードします。
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
            settings.SeedDefaultRules();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        // 既定の Assets/AssetResource/Local(=Local) / Remote(=Remote) が在る場合のみルール化する。
        private void SeedDefaultRules()
        {
            var localFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultLocalFolderPath);
            if (localFolder != null)
            {
                _syncRules.Add(new AssetVaultSyncRule(localFolder, AssetVaultDeliveryMode.Local));
            }

            var remoteFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultRemoteFolderPath);
            if (remoteFolder != null)
            {
                _syncRules.Add(new AssetVaultSyncRule(remoteFolder, AssetVaultDeliveryMode.Remote));
            }
        }
    }
}
