using UnityEditor;
using UnityEngine;

namespace UniLab.AssetDelivery.Editor
{
    /// <summary>
    /// Provides UniLab menu commands for switching the active Addressables profile.
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
        /// Switches the active Addressables profile to the development environment.
        /// </summary>
        [MenuItem(DevelopmentMenuPath)]
        public static void SwitchToDevelopment()
        {
            SwitchProfile(Development);
        }

        /// <summary>
        /// Switches the active Addressables profile to the staging environment.
        /// </summary>
        [MenuItem(StagingMenuPath)]
        public static void SwitchToStaging()
        {
            SwitchProfile(Staging);
        }

        /// <summary>
        /// Switches the active Addressables profile to the production environment.
        /// </summary>
        [MenuItem(ProductionMenuPath)]
        public static void SwitchToProduction()
        {
            SwitchProfile(Production);
        }

        /// <summary>
        /// Switches the active Addressables profile by profile name.
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
