using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// 有効な Addressables settings asset への共有アクセスを提供します。
    /// </summary>
    internal static class AddressableSettingsAccessor
    {
        private const string SettingsMissingMessage = "Addressables settings are not initialized.";

        /// <summary>
        /// 初期化済みの場合に、有効な Addressables settings asset を取得します。
        /// </summary>
        public static bool TryGetSettings(out AddressableAssetSettings settings)
        {
            settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError(SettingsMissingMessage);
                return false;
            }

            return true;
        }
    }
}
