using UnityEditor;
using UnityEngine;

namespace UniLab.AssetDelivery.Editor
{
    /// <summary>
    /// 有効な Addressables profile を切り替える UniLab メニューコマンドを提供します。
    /// </summary>
    public static class AssetDeliveryProfileSwitcher
    {
        private const string BaseMenuPath = "UniLab/AssetDelivery/Profile/";
        private const string Development = "dev";
        private const string Staging = "staging";
        private const string Production = "prod";
        private const string DevelopmentMenuPath = BaseMenuPath + "Development";
        private const string StagingMenuPath = BaseMenuPath + "Staging";
        private const string ProductionMenuPath = BaseMenuPath + "Production";
        private const string ProfileMissingMessageFormat = "Addressables profile was not found: {0}";
        private const string ProfileSwitchedMessageFormat = "Addressables active profile switched to: {0}";

        /// <summary>
        /// 有効な Addressables profile を開発環境に切り替えます。
        /// </summary>
        [MenuItem(DevelopmentMenuPath)]
        public static void SwitchToDevelopment()
        {
            SwitchProfile(Development);
        }

        /// <summary>
        /// 有効な Addressables profile をステージング環境に切り替えます。
        /// </summary>
        [MenuItem(StagingMenuPath)]
        public static void SwitchToStaging()
        {
            SwitchProfile(Staging);
        }

        /// <summary>
        /// 有効な Addressables profile を本番環境に切り替えます。
        /// </summary>
        [MenuItem(ProductionMenuPath)]
        public static void SwitchToProduction()
        {
            SwitchProfile(Production);
        }

        /// <summary>
        /// profile 名を指定して、有効な Addressables profile を切り替えます。
        /// </summary>
        public static void SwitchProfile(string profileName)
        {
            if (!AddressableSettingsAccessor.TryGetSettings(out var settings))
            {
                return;
            }

            var profileId = settings.profileSettings.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                Debug.LogError(string.Format(ProfileMissingMessageFormat, profileName));
                return;
            }

            settings.activeProfileId = profileId;
            Debug.Log(string.Format(ProfileSwitchedMessageFormat, profileName));
        }
    }
}
