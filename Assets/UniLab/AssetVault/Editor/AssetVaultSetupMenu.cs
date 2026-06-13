using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetResource のフォルダ規約から Addressables を自動構成するメニューコマンドを提供します。
    /// </summary>
    public static class AssetVaultSetupMenu
    {
        private const string BaseMenuPath = "UniLab/AssetVault/Setup/";
        private const string SyncAssetResourceMenuPath = BaseMenuPath + "Sync AssetResource";
        private const string OpenSetupSettingsMenuPath = BaseMenuPath + "Open Setup Settings";
        private const string LocalBuildPathVariableName = "LocalBuildPath";
        private const string LocalLoadPathVariableName = "LocalLoadPath";
        private const string RemoteBuildPathVariableName = "RemoteBuildPath";
        private const string RemoteLoadPathVariableName = "RemoteLoadPath";
        private const string LocalBuildPathValue = "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]";
        private const string LocalLoadPathValue = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]";
        private const string RemoteBuildPathValue = "ServerData/[BuildTarget]";
        private const string BuildTargetToken = "[BuildTarget]";
        private const string BaseUrlPropertyName = "BaseUrl";
        private const string ContentPathPropertyName = "ContentPath";
        private const string LocalGroupPrefix = "Local_";
        private const string RemoteGroupPrefix = "Remote_";
        private const string InternalFolderName = "Internal";
        private const string ExternalFolderName = "External";
        private const string DefaultLocalGroupName = LocalGroupPrefix + InternalFolderName;
        private const string DefaultRemoteGroupName = RemoteGroupPrefix + ExternalFolderName;
        private const string CategorySkippedMessageFormat = "AssetVault setup skipped because folder was not found: {0}";
        private const string DuplicateAddressWarningLineFormat = "重複アドレス: {0}（{1} と {2}）";
        private const string SyncCompletedMessage = "AssetVault Addressables setup completed.";

        /// <summary>
        /// AssetResource の Internal / External フォルダ規約を Addressables に同期します。
        /// </summary>
        [MenuItem(SyncAssetResourceMenuPath)]
        public static void SyncAssetResource()
        {
            if (!AddressableSettingsAccessor.TryGetSettings(out var settings))
            {
                return;
            }

            EnsureProfileValue(settings, LocalBuildPathVariableName, LocalBuildPathValue);
            EnsureProfileValue(settings, LocalLoadPathVariableName, LocalLoadPathValue);
            EnsureProfileValue(settings, RemoteBuildPathVariableName, RemoteBuildPathValue);
            EnsureProfileValue(settings, RemoteLoadPathVariableName, CreateRemoteLoadPath());

            settings.profileSettings.SetValue(settings.activeProfileId, RemoteLoadPathVariableName, CreateRemoteLoadPath());
            settings.profileSettings.SetValue(settings.activeProfileId, RemoteBuildPathVariableName, RemoteBuildPathValue);

            var assetVaultSetupSettings = AssetVaultSetupSettings.GetOrCreate();
            var rootPath = NormalizeAssetPath(assetVaultSetupSettings.RootPath);
            var duplicateAddressCollector = new DuplicateAddressCollector();
            SyncCategory(settings, rootPath + "/" + InternalFolderName, true, duplicateAddressCollector);
            SyncCategory(settings, rootPath + "/" + ExternalFolderName, false, duplicateAddressCollector);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            duplicateAddressCollector.LogWarning();
            Debug.Log(SyncCompletedMessage);
        }

        /// <summary>
        /// AssetVaultSetupSettings を選択して Inspector で開きます。
        /// </summary>
        [MenuItem(OpenSetupSettingsMenuPath)]
        public static void OpenSetupSettings()
        {
            Selection.activeObject = AssetVaultSetupSettings.GetOrCreate();
        }

        private static void SyncCategory(
            AddressableAssetSettings settings,
            string categoryRoot,
            bool isLocal,
            DuplicateAddressCollector duplicateAddressCollector)
        {
            if (!AssetDatabase.IsValidFolder(categoryRoot))
            {
                Debug.Log(string.Format(CategorySkippedMessageFormat, categoryRoot));
                return;
            }

            var subFolders = AssetDatabase.GetSubFolders(categoryRoot);
            foreach (var subFolder in subFolders)
            {
                var groupName = GetGroupName(subFolder, isLocal);
                var group = EnsureGroup(settings, groupName, isLocal);
                RegisterFolder(settings, group, subFolder, categoryRoot, duplicateAddressCollector);
            }

            RegisterDirectAssets(settings, categoryRoot, isLocal, duplicateAddressCollector);
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName, bool isLocal)
        {
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    false,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            ConfigureBundledAssetGroupSchema(settings, group, isLocal);
            ConfigureContentUpdateGroupSchema(group, isLocal);
            return group;
        }

        private static void RegisterFolder(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string folder,
            string categoryRoot,
            DuplicateAddressCollector duplicateAddressCollector)
        {
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            foreach (var guid in guids)
            {
                RegisterAsset(settings, group, guid, categoryRoot, duplicateAddressCollector);
            }
        }

        private static void RegisterDirectAssets(
            AddressableAssetSettings settings,
            string categoryRoot,
            bool isLocal,
            DuplicateAddressCollector duplicateAddressCollector)
        {
            var group = default(AddressableAssetGroup);
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { categoryRoot });
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                var directoryPath = NormalizeAssetPath(Path.GetDirectoryName(assetPath));
                if (directoryPath != categoryRoot)
                {
                    continue;
                }

                if (group == null)
                {
                    var defaultGroupName = isLocal ? DefaultLocalGroupName : DefaultRemoteGroupName;
                    group = EnsureGroup(settings, defaultGroupName, isLocal);
                }

                RegisterAsset(settings, group, guid, categoryRoot, duplicateAddressCollector);
            }
        }

        private static void RegisterAsset(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string guid,
            string categoryRoot,
            DuplicateAddressCollector duplicateAddressCollector)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            if (entry == null)
            {
                return;
            }

            entry.address = CreateAddress(assetPath, categoryRoot);
            duplicateAddressCollector.Record(entry.address, assetPath);
        }

        private static void ConfigureBundledAssetGroupSchema(AddressableAssetSettings settings, AddressableAssetGroup group, bool isLocal)
        {
            var bundledAssetGroupSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundledAssetGroupSchema == null)
            {
                bundledAssetGroupSchema = group.AddSchema<BundledAssetGroupSchema>();
            }

            var buildPathVariableName = isLocal ? LocalBuildPathVariableName : RemoteBuildPathVariableName;
            var loadPathVariableName = isLocal ? LocalLoadPathVariableName : RemoteLoadPathVariableName;
            bundledAssetGroupSchema.BuildPath.SetVariableByName(settings, buildPathVariableName);
            bundledAssetGroupSchema.LoadPath.SetVariableByName(settings, loadPathVariableName);
            bundledAssetGroupSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
        }

        private static void ConfigureContentUpdateGroupSchema(AddressableAssetGroup group, bool isLocal)
        {
            var contentUpdateGroupSchema = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentUpdateGroupSchema == null)
            {
                contentUpdateGroupSchema = group.AddSchema<ContentUpdateGroupSchema>();
            }

            contentUpdateGroupSchema.StaticContent = isLocal;
        }

        private static void EnsureProfileValue(AddressableAssetSettings settings, string variableName, string defaultValue)
        {
            var variableNames = settings.profileSettings.GetVariableNames();
            if (variableNames.Contains(variableName))
            {
                return;
            }

            settings.profileSettings.CreateValue(variableName, defaultValue);
        }

        private static string CreateRemoteLoadPath()
        {
            var runtimeTypeName = typeof(UniLab.AssetVault.AssetVaultRuntime).FullName;
            return $"{{{runtimeTypeName}.{BaseUrlPropertyName}}}/{{{runtimeTypeName}.{ContentPathPropertyName}}}/{BuildTargetToken}";
        }

        private static string CreateAddress(string assetPath, string categoryRoot)
        {
            var relativePath = assetPath.Substring(categoryRoot.Length + "/".Length);
            var extension = Path.GetExtension(relativePath);
            if (string.IsNullOrEmpty(extension))
            {
                return relativePath;
            }

            return relativePath.Substring(0, relativePath.Length - extension.Length);
        }

        private static string GetGroupName(string folderPath, bool isLocal)
        {
            var groupPrefix = isLocal ? LocalGroupPrefix : RemoteGroupPrefix;
            return groupPrefix + Path.GetFileName(folderPath);
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            if (assetPath == null || assetPath == "")
            {
                return "";
            }

            return assetPath.Replace("\\", "/").TrimEnd('/');
        }

        private sealed class DuplicateAddressCollector
        {
            private readonly Dictionary<string, string> _registeredAssetPathsByAddress = new Dictionary<string, string>();
            private readonly List<DuplicateAddress> _duplicateAddresses = new List<DuplicateAddress>();

            public void Record(string address, string assetPath)
            {
                if (!_registeredAssetPathsByAddress.TryGetValue(address, out var registeredAssetPath))
                {
                    _registeredAssetPathsByAddress.Add(address, assetPath);
                    return;
                }

                if (registeredAssetPath == assetPath)
                {
                    return;
                }

                _duplicateAddresses.Add(new DuplicateAddress(address, registeredAssetPath, assetPath));
            }

            public void LogWarning()
            {
                if (_duplicateAddresses.Count <= 0)
                {
                    return;
                }

                var warningLines = new List<string>();
                foreach (var duplicateAddress in _duplicateAddresses)
                {
                    warningLines.Add(string.Format(
                        DuplicateAddressWarningLineFormat,
                        duplicateAddress.Address,
                        duplicateAddress.FirstAssetPath,
                        duplicateAddress.DuplicateAssetPath));
                }

                Debug.LogWarning(string.Join("\n", warningLines));
            }
        }

        private readonly struct DuplicateAddress
        {
            public DuplicateAddress(string address, string firstAssetPath, string duplicateAssetPath)
            {
                Address = address;
                FirstAssetPath = firstAssetPath;
                DuplicateAssetPath = duplicateAssetPath;
            }

            public string Address { get; }
            public string FirstAssetPath { get; }
            public string DuplicateAssetPath { get; }
        }
    }
}
