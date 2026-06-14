using UnityEngine;

namespace UniLab.AssetVault.Debugging
{
    /// <summary>
    /// デバッグ環境プリセットを起動時に <see cref="AssetVaultRuntime"/> へ反映するブートストラップです。
    /// UNITY_EDITOR || DEVELOPMENT_BUILD のアセンブリに属するため、release ビルドでは丸ごとストリップされます。
    /// Editor Play でも development プレイヤービルドでも同じ経路（BeforeSceneLoad）で適用されます。
    /// </summary>
    public static class AssetVaultDebugBootstrap
    {
        private const string PresetMissingMessageFormat = "AssetVault Debug Override is enabled but preset '{0}' was not found. Overrides were skipped.";
        private const string PresetEmptyMessage = "AssetVault Debug Override is enabled but no presets are registered. Overrides were skipped.";

        // アプリ初期化（IAssetVaultService.InitializeAsync）より前に値を入れる狙いで BeforeSceneLoad を使う。
        // ただしアプリ初期化との厳密な前後は保証されないため、アプリ側が config から設定する場合はそちらが優先される（順序はアプリ責務）。
        // 上書きするのは BaseUrl のみ。ContentPath（版）は version.json 解決に任せるため一切触らない。
        // ドメインリロード無効時は static の AssetVaultRuntime.BaseUrl に前回値が残るため、無効・未解決時は null クリアしてリークを防ぐ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            var settings = AssetVaultDebugEnvironmentSettings.Load();
            if (settings == null || !settings.OverrideEnabled)
            {
                AssetVaultRuntime.BaseUrl = null;
                return;
            }

            var preset = settings.ResolveSelectedPreset();
            if (preset == null)
            {
                AssetVaultRuntime.BaseUrl = null;
                var selectedName = settings.SelectedPresetName;
                Debug.LogWarning(string.IsNullOrEmpty(selectedName)
                    ? PresetEmptyMessage
                    : string.Format(PresetMissingMessageFormat, selectedName));
                return;
            }

            AssetVaultRuntime.BaseUrl = preset.BaseUrl;
        }
    }
}
