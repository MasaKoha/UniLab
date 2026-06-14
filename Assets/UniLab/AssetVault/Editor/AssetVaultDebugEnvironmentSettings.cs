using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// QA 用デバッグ環境プリセットの一覧を保持する ScriptableObject です。
    /// Debug Override のドロップダウンの選択肢になり、選択値は Play 時に <see cref="AssetVaultRuntime"/> へ反映されます。
    /// env → URL のマッピングはアプリ config が正ですが、これは QA がエディタから即切り替えるための独立したデバッグ用テーブルです。
    /// </summary>
    public sealed class AssetVaultDebugEnvironmentSettings : ScriptableObject
    {
        /// <summary>
        /// 設定アセットの保存先パスです。
        /// </summary>
        public const string AssetPath = GeneratedAssetFolder.Path + "/AssetVaultDebugEnvironmentSettings.asset";

        private const string DefaultContentPath = "latest";

        [SerializeField] private List<AssetVaultDebugEnvironmentPreset> _presets = new List<AssetVaultDebugEnvironmentPreset>();

        /// <summary>
        /// 登録済みのデバッグ環境プリセット一覧です。
        /// </summary>
        public IReadOnlyList<AssetVaultDebugEnvironmentPreset> Presets => _presets;

        /// <summary>
        /// 設定アセットを取得し、存在しない場合は既定プリセット付きで作成します。
        /// </summary>
        public static AssetVaultDebugEnvironmentSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AssetVaultDebugEnvironmentSettings>(AssetPath);
            if (settings != null)
            {
                return settings;
            }

            GeneratedAssetFolder.Ensure();

            settings = CreateInstance<AssetVaultDebugEnvironmentSettings>();
            settings.SeedDefaults();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        // TODO: 既定の BaseUrl は実際の CDN ホストに置き換えること。新規作成時の雛形として 3 環境をシードする。
        private void SeedDefaults()
        {
            _presets.Add(new AssetVaultDebugEnvironmentPreset("Development", "https://dev.example.com/app", DefaultContentPath));
            _presets.Add(new AssetVaultDebugEnvironmentPreset("Staging", "https://stg.example.com/app", DefaultContentPath));
            _presets.Add(new AssetVaultDebugEnvironmentPreset("Production", "https://prod.example.com/app", DefaultContentPath));
        }
    }
}
