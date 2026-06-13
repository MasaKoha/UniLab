using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UniLab.AssetDelivery.Editor
{
    /// <summary>
    /// Addressables player content build 用の UniLab メニューコマンドを提供します。
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
        /// 有効な Addressables settings を使って、新しい Addressables player content をビルドします。
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
        /// 前回の content state file から Addressables content update をビルドします。
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
