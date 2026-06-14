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

        // 直近のこのブートストラップが BaseUrl を上書きしたか。アプリが設定した値を消さないよう、
        // 「自分が入れた値」だけをクリア対象にするためのフラグ（ドメインリロード無効時も含めセッション跨ぎで一貫させる）。
        private static bool _applied;

        // アプリ初期化（IAssetVaultService.InitializeAsync）より前に値を入れる狙いで BeforeSceneLoad を使う。
        // ただしアプリ初期化との厳密な前後は保証されないため、アプリ側が config から設定する場合はそちらが優先される（順序はアプリ責務）。
        // 上書きするのは BaseUrl のみ。ContentPath（版）は version.json 解決に任せるため一切触らない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            var settings = AssetVaultDebugEnvironmentSettings.Load();
            if (settings == null || !settings.OverrideEnabled)
            {
                ClearIfApplied();
                return;
            }

            var preset = settings.ResolveSelectedPreset();
            if (preset == null)
            {
                ClearIfApplied();
                var selectedName = settings.SelectedPresetName;
                Debug.LogWarning(string.IsNullOrEmpty(selectedName)
                    ? PresetEmptyMessage
                    : string.Format(PresetMissingMessageFormat, selectedName));
                return;
            }

            AssetVaultRuntime.BaseUrl = preset.BaseUrl;
            _applied = true;
        }

        // 自分が上書きした場合のみ null へ戻す。ドメインリロード有効時は _applied が初期化されるため、
        // アプリが設定した BaseUrl を消すことはない。無効時のみ前回の上書き残留をクリアする。
        private static void ClearIfApplied()
        {
            if (!_applied)
            {
                return;
            }

            AssetVaultRuntime.BaseUrl = null;
            _applied = false;
        }
    }
}
