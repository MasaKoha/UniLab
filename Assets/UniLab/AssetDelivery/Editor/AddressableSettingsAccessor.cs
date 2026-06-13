using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UniLab.AssetDelivery.Editor
{
    /// <summary>
    /// Provides shared access to the active Addressables settings asset.
    /// </summary>
    internal static class AddressableSettingsAccessor
    {
        private const string SettingsMissingMessage = "Addressables settings are not initialized.";

        /// <summary>
        /// Gets the active Addressables settings asset when it is initialized.
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
