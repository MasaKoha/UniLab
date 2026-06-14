using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault の Addressables 操作（ビルド・AssetResource 同期・状態取得）を集約する操作レイヤです。
    /// EditorWindow と MenuItem の双方がここを呼ぶことで、UI と操作を分離します。
    /// </summary>
    public static class AssetVaultEditorOperations
    {
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
        private const string CategorySkippedMessageFormat = "AssetVault setup skipped because folder was not found: {0}";
        private const string RuleSkippedMessage = "AssetVault setup skipped a rule with an unset or non-folder reference.";
        private const string DuplicateAddressWarningLineFormat = "重複アドレス: {0}（{1} と {2}）";
        private const string SyncCompletedMessage = "AssetVault Addressables setup completed.";
        private const string ContentStateMissingMessage = "Addressables content state file was not found. Run a new build before a content update build.";
        private const string NewBuildFailedMessage = "Addressables new build failed.";
        private const string ContentUpdateFailedMessage = "Addressables content update build failed.";
        private const string NewBuildCompletedMessage = "Addressables new build completed.";
        private const string ContentUpdateCompletedMessage = "Addressables content update build completed.";

        // --- Build ---

        /// <summary>
        /// 有効な Addressables settings を使って、新しい Addressables player content をビルドします。
        /// </summary>
        /// <returns>ビルドに成功した場合は true。</returns>
        public static bool BuildNew()
        {
            if (!AddressableSettingsAccessor.TryGetSettings(out _))
            {
                return false;
            }

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (result == null)
            {
                Debug.LogError(NewBuildFailedMessage);
                return false;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError(result.Error);
                return false;
            }

            Debug.Log(NewBuildCompletedMessage);
            return true;
        }

        /// <summary>
        /// 前回の content state file から Addressables content update をビルドします。
        /// </summary>
        /// <returns>ビルドに成功した場合は true。</returns>
        public static bool BuildContentUpdate()
        {
            if (!AddressableSettingsAccessor.TryGetSettings(out var settings))
            {
                return false;
            }

            var contentStatePath = ContentUpdateScript.GetContentStateDataPath(false);
            if (!File.Exists(contentStatePath))
            {
                Debug.LogError(ContentStateMissingMessage);
                return false;
            }

            var result = ContentUpdateScript.BuildContentUpdate(settings, contentStatePath);
            if (result == null)
            {
                Debug.LogError(ContentUpdateFailedMessage);
                return false;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError(result.Error);
                return false;
            }

            Debug.Log(ContentUpdateCompletedMessage);
            return true;
        }

        // --- Setup ---

        /// <summary>
        /// 設定アセットの同期ルール（フォルダ＋Local/Remote）に従って Addressables を構成します。
        /// </summary>
        /// <returns>同期に成功した場合は true。</returns>
        public static bool SyncAssetResource()
        {
            if (!AddressableSettingsAccessor.TryGetSettings(out var settings))
            {
                return false;
            }

            EnsureProfileValue(settings, LocalBuildPathVariableName, LocalBuildPathValue);
            EnsureProfileValue(settings, LocalLoadPathVariableName, LocalLoadPathValue);
            EnsureProfileValue(settings, RemoteBuildPathVariableName, RemoteBuildPathValue);
            EnsureProfileValue(settings, RemoteLoadPathVariableName, CreateRemoteLoadPath());

            // RemoteLoadPath / RemoteBuildPath は AssetVaultRuntime のトークン規約・ServerData 規約に毎回追従させるため、
            // 既存値があっても上書きする（Local 側は EnsureProfileValue で初回のみ。Remote だけ非対称に上書きするのは意図的）。
            settings.profileSettings.SetValue(settings.activeProfileId, RemoteLoadPathVariableName, CreateRemoteLoadPath());
            settings.profileSettings.SetValue(settings.activeProfileId, RemoteBuildPathVariableName, RemoteBuildPathValue);

            var assetVaultSetupSettings = AssetVaultSetupSettings.GetOrCreate();
            var duplicateAddressCollector = new DuplicateAddressCollector();
            foreach (var rule in assetVaultSetupSettings.SyncRules)
            {
                SyncRule(settings, rule, duplicateAddressCollector);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            duplicateAddressCollector.LogWarning();
            Debug.Log(SyncCompletedMessage);
            return true;
        }

        /// <summary>
        /// AssetVaultSetupSettings を選択して Inspector で開きます。
        /// </summary>
        public static void OpenSetupSettings()
        {
            Selection.activeObject = AssetVaultSetupSettings.GetOrCreate();
        }

        // --- Status ---

        /// <summary>
        /// AssetVault の Addressables 構成の現状スナップショットを取得します（ダッシュボード表示用）。
        /// </summary>
        public static AssetVaultStatus GetStatus()
        {
            var syncRules = AssetVaultSetupSettings.GetOrCreate().SyncRules;
            var syncRuleCount = syncRules.Count;
            var validFolderCount = syncRules.Count(rule => rule != null && rule.ResolveFolderPath() != null);

            if (!AddressableSettingsAccessor.TryGetSettingsSilently(out var settings))
            {
                return new AssetVaultStatus(false, string.Empty, 0, 0, syncRuleCount, validFolderCount);
            }

            var remoteLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, RemoteLoadPathVariableName);
            var localGroupCount = settings.groups.Count(group => group != null && group.Name.StartsWith(LocalGroupPrefix, System.StringComparison.Ordinal));
            var remoteGroupCount = settings.groups.Count(group => group != null && group.Name.StartsWith(RemoteGroupPrefix, System.StringComparison.Ordinal));

            return new AssetVaultStatus(
                true,
                remoteLoadPath ?? string.Empty,
                localGroupCount,
                remoteGroupCount,
                syncRuleCount,
                validFolderCount);
        }

        // --- Internal helpers ---

        private static void SyncRule(
            AddressableAssetSettings settings,
            AssetVaultSyncRule rule,
            DuplicateAddressCollector duplicateAddressCollector)
        {
            if (rule == null)
            {
                return;
            }

            var folderPath = rule.ResolveFolderPath();
            if (folderPath == null)
            {
                Debug.Log(RuleSkippedMessage);
                return;
            }

            SyncCategory(settings, folderPath, rule.IsLocal, duplicateAddressCollector);
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
                    // ルートフォルダ直下のアセットは、そのフォルダ名から作る既定グループにまとめる。
                    group = EnsureGroup(settings, GetGroupName(categoryRoot, isLocal), isLocal);
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
            var runtimeTypeName = typeof(AssetVaultRuntime).FullName;
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
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            return assetPath.Replace("\\", "/").TrimEnd('/');
        }

        /// <summary>
        /// 同一アドレスへ別アセットが二重登録された場合を収集し、まとめて警告ログに出します。
        /// </summary>
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
