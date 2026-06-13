using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UniLab.AssetDelivery.Editor
{
    /// <summary>
    /// Provides UniLab menu commands for Addressables player content builds.
    /// </summary>
    public static class AssetDeliveryBuildMenu
    {
        private const string BaseMenuPath = "UniLab/AssetDelivery/Build/";
        private const string NewBuildMenuPath = BaseMenuPath + "New Build";
        private const string ContentUpdateMenuPath = BaseMenuPath + "Update a Previous Build (Diff)";
        private const string ContentStateMissingMessage = "Addressables content state file was not found. Run a new build before a content update build.";
        private const string NewBuildFailedMessage = "Addressables new build failed.";
        private const string ContentUpdateFailedMessage = "Addressables content update build failed.";
        private const string NewBuildCompletedMessage = "Addressables new build completed.";
        private const string ContentUpdateCompletedMessage = "Addressables content update build completed.";

        /// <summary>
        /// Builds new Addressables player content using the active Addressables settings.
        /// </summary>
        [MenuItem(NewBuildMenuPath)]
        public static void BuildNew()
        {
            if (!AddressableSettingsAccessor.TryGetSettings(out var settings))
            {
                return;
            }

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (result == null)
            {
                Debug.LogError(NewBuildFailedMessage);
                return;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError(result.Error);
                return;
            }

            Debug.Log(NewBuildCompletedMessage);
        }

        /// <summary>
        /// Builds an Addressables content update from the previous content state file.
        /// </summary>
        [MenuItem(ContentUpdateMenuPath)]
        public static void BuildContentUpdate()
        {
            if (!AddressableSettingsAccessor.TryGetSettings(out var settings))
            {
                return;
            }

            var contentStatePath = ContentUpdateScript.GetContentStateDataPath(false);
            if (!File.Exists(contentStatePath))
            {
                Debug.LogError(ContentStateMissingMessage);
                return;
            }

            var result = ContentUpdateScript.BuildContentUpdate(settings, contentStatePath);
            if (result == null)
            {
                Debug.LogError(ContentUpdateFailedMessage);
                return;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError(result.Error);
                return;
            }

            Debug.Log(ContentUpdateCompletedMessage);
        }
    }
}
