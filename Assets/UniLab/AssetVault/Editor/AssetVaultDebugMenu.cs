using UniLab.AssetVault.Debugging;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// Debug Override の有効化・無効化を行うエディタメニューです。
    /// 選択は UI のトグル/ドロップダウンではなく、ここ（コード）から <see cref="AssetVaultDebugEnvironmentSettings.Activate"/> 等を呼びます。
    /// </summary>
    public static class AssetVaultDebugMenu
    {
        private const string SelectMenuPath = "UniLab/AssetVault/Debug/Select Environment...";
        private const string DisableMenuPath = "UniLab/AssetVault/Debug/Disable Override";
        private const string DisableItemLabel = "Disable Override";
        private const string NoPresetsMessage = "プリセットが未登録です。Dashboard の Edit Presets から追加してください。";

        /// <summary>
        /// 登録済みプリセットをドロップダウンで提示し、選んだものを有効化します。
        /// </summary>
        [MenuItem(SelectMenuPath)]
        private static void SelectEnvironment()
        {
            var settings = AssetVaultDebugEnvironmentSettings.GetOrCreate();
            var presets = settings.Presets;
            if (presets.Count <= 0)
            {
                Debug.LogWarning(NoPresetsMessage);
                return;
            }

            var menu = new GenericMenu();
            foreach (var preset in presets)
            {
                // ループ変数を直接キャプチャしないようローカルへ退避する。
                var presetName = preset.DisplayName;
                var isActive = settings.OverrideEnabled && settings.SelectedPresetName == presetName;
                menu.AddItem(new GUIContent(presetName), isActive, () => settings.Activate(presetName));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(DisableItemLabel), !settings.OverrideEnabled, settings.Deactivate);
            menu.ShowAsContext();
        }

        /// <summary>
        /// 上書きを無効化します。
        /// </summary>
        [MenuItem(DisableMenuPath)]
        private static void DisableOverride()
        {
            AssetVaultDebugEnvironmentSettings.GetOrCreate().Deactivate();
        }
    }
}
