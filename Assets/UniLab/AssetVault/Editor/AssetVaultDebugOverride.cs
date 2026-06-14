using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// QA 用のデバッグ上書き機能です。Play 突入時に、選択した環境プリセットの値で <see cref="AssetVaultRuntime"/> の
    /// BaseUrl / ContentPath を上書きし、「prod アプリで dev のアセットを見る」「特定の版フォルダを読む」を実現します。
    /// プリセット定義は <see cref="AssetVaultDebugEnvironmentSettings"/>（ScriptableObject）が持ち、
    /// 有効/無効と選択プリセット名のみエディタ状態として EditorPrefs に保持します（ランタイムにデバッグ専用 API は足しません）。
    /// </summary>
    [InitializeOnLoad]
    public static class AssetVaultDebugOverride
    {
        private const string EnabledPrefKey = "UniLab.AssetVault.DebugOverride.Enabled";
        private const string SelectedPresetNamePrefKey = "UniLab.AssetVault.DebugOverride.SelectedPresetName";
        private const string PresetMissingMessageFormat = "AssetVault Debug Override is enabled but preset '{0}' was not found. Overrides were skipped.";
        private const string PresetEmptyMessage = "AssetVault Debug Override is enabled but no presets are registered. Overrides were skipped.";

        static AssetVaultDebugOverride()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>デバッグ上書きを有効にするかどうかです。</summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, false);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        /// <summary>
        /// 上書きに使うプリセットの表示名です。実体は <see cref="AssetVaultDebugEnvironmentSettings"/> から名前で引きます。
        /// </summary>
        public static string SelectedPresetName
        {
            get => EditorPrefs.GetString(SelectedPresetNamePrefKey, string.Empty);
            set => EditorPrefs.SetString(SelectedPresetNamePrefKey, value);
        }

        /// <summary>
        /// 現在選択中のプリセットを解決します。未登録・未選択・名称不一致の場合は null を返します。
        /// </summary>
        public static AssetVaultDebugEnvironmentPreset ResolveSelectedPreset()
        {
            var presets = AssetVaultDebugEnvironmentSettings.GetOrCreate().Presets;
            if (presets.Count <= 0)
            {
                return null;
            }

            var selectedName = SelectedPresetName;
            // 未選択時は先頭プリセットを既定とする（初回利用でも何かしら反映できるように）。
            if (string.IsNullOrEmpty(selectedName))
            {
                return presets[0];
            }

            return presets.FirstOrDefault(preset => preset.DisplayName == selectedName);
        }

        // EnteredPlayMode（ドメインリロード後）で反映する。アプリ初期化との前後は保証されないため、
        // アプリ側が config から値を設定する場合はそちらが優先される（順序はアプリ責務）。
        // ドメインリロード無効時は static フィールド（AssetVaultRuntime 側の値）が Play セッション間で
        // 残留するため、無効時は null クリアして前回のデバッグ値リークを防ぐ。
        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            if (!Enabled)
            {
                AssetVaultRuntime.BaseUrl = null;
                AssetVaultRuntime.ContentPath = null;
                return;
            }

            var preset = ResolveSelectedPreset();
            if (preset == null)
            {
                var selectedName = SelectedPresetName;
                Debug.LogWarning(string.IsNullOrEmpty(selectedName)
                    ? PresetEmptyMessage
                    : string.Format(PresetMissingMessageFormat, selectedName));
                return;
            }

            AssetVaultRuntime.BaseUrl = preset.BaseUrl;
            AssetVaultRuntime.ContentPath = preset.ContentPath;
        }
    }
}
