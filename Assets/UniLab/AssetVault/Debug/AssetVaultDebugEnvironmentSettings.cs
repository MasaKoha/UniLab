using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UniLab.AssetVault.Debugging
{
    /// <summary>
    /// QA 用デバッグ環境プリセットと、その有効/選択状態を保持する ScriptableObject です。
    /// development ビルド・Editor Play でのみ存在するアセンブリ（UNITY_EDITOR || DEVELOPMENT_BUILD）に属し、
    /// release ビルドからはコードごとストリップされます。実行時は <see cref="Load"/> で Resources からロードします。
    /// </summary>
    public sealed class AssetVaultDebugEnvironmentSettings : ScriptableObject
    {
        /// <summary>
        /// Resources.Load のキー（拡張子・パスなし）です。正本は Resources 外に置き、development ビルド時のみ
        /// <see cref="AssetVaultDebugBuildProcessor"/> がこのキーで Resources へ一時コピーします（release では同梱しない）。
        /// </summary>
        public const string ResourceName = "AssetVaultDebugEnvironmentSettings";

        private const string DefaultContentPath = "latest";

        [SerializeField] private bool _overrideEnabled;
        [SerializeField] private string _selectedPresetName = string.Empty;
        [SerializeField] private List<AssetVaultDebugEnvironmentPreset> _presets = new List<AssetVaultDebugEnvironmentPreset>();

        /// <summary>上書きを適用するかどうかです（実機 dev ビルドにも焼き込まれます）。</summary>
        public bool OverrideEnabled
        {
            get => _overrideEnabled;
            set => _overrideEnabled = value;
        }

        /// <summary>適用するプリセットの表示名です。</summary>
        public string SelectedPresetName
        {
            get => _selectedPresetName;
            set => _selectedPresetName = value;
        }

        /// <summary>登録済みのデバッグ環境プリセット一覧です。</summary>
        public IReadOnlyList<AssetVaultDebugEnvironmentPreset> Presets => _presets;

        /// <summary>
        /// 現在選択中のプリセットを解決します。未登録・名称不一致は null、未選択時は先頭プリセットを返します。
        /// </summary>
        public AssetVaultDebugEnvironmentPreset ResolveSelectedPreset()
        {
            if (_presets.Count <= 0)
            {
                return null;
            }

            if (string.IsNullOrEmpty(_selectedPresetName))
            {
                return _presets[0];
            }

            return _presets.FirstOrDefault(preset => preset.DisplayName == _selectedPresetName);
        }

        /// <summary>
        /// 実行時に設定をロードします。Editor では正本アセットを直接、プレイヤービルドでは Resources からロードします。
        /// development ビルドでのみ Resources に同梱されるため、release では null（=上書きなし）になります。
        /// </summary>
        public static AssetVaultDebugEnvironmentSettings Load()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<AssetVaultDebugEnvironmentSettings>(AssetPath);
#else
            return Resources.Load<AssetVaultDebugEnvironmentSettings>(ResourceName);
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 正本アセットの保存先パスです。Resources の「外」に置くことで、何もしなければプレイヤービルドに同梱されません。
        /// development ビルド時のみ <see cref="AssetVaultDebugBuildProcessor"/> が Resources へ複製します。
        /// </summary>
        public const string AssetPath = "Assets/UniLab/AssetVault/Debug/" + ResourceName + ".asset";

        /// <summary>
        /// 設定アセットを取得し、存在しない場合は既定プリセット付きで作成します（Editor 専用）。
        /// </summary>
        public static AssetVaultDebugEnvironmentSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AssetVaultDebugEnvironmentSettings>(AssetPath);
            if (settings != null)
            {
                return settings;
            }

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
#endif
    }
}
