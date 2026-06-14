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
        private const string CategorySkippedMessageFormat = "AssetVault setup skipped because folder was not found: {0}";
        private const string LocalFolderMissingMessage = "AssetVault setup aborted: the Local folder is required but not set. Assign it in AssetVaultSetupSettings.";
        private const string ProfileVariableMissingMessageFormat = "Addressables profile variable '{0}' was not found; group build/load path may be misconfigured.";
        private const string DuplicateAddressFailureMessage = "AssetVault setup failed due to duplicate addresses. Resolve the following collisions and run sync again:\n{0}";
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

            AddressablesPlayerBuildResult result;
            try
            {
                AddressableAssetSettings.BuildPlayerContent(out result);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"{NewBuildFailedMessage} {exception.Message}");
                return false;
            }

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

            AddressablesPlayerBuildResult result;
            try
            {
                result = ContentUpdateScript.BuildContentUpdate(settings, contentStatePath);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"{ContentUpdateFailedMessage} {exception.Message}");
                return false;
            }

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

        /// <summary>
        /// Content Update ビルドが可能か（前回の content state file が存在するか）を返します。UI のボタン制御に使います。
        /// </summary>
        public static bool CanBuildContentUpdate()
        {
            return File.Exists(ContentUpdateScript.GetContentStateDataPath(false));
        }

        // --- Setup ---

        /// <summary>
        /// 設定アセットの Local（必須）/ Remote（任意）フォルダに従って Addressables を構成します。
        /// 今回の同期で登録されなかった管理グループ（Local_/Remote_）の古いエントリ・空グループは掃除します。
        /// </summary>
        /// <returns>同期に成功した場合は true。重複アドレス検出時は false。</returns>
        public static bool SyncAssetResource()
        {
            if (!AddressableSettingsAccessor.TryGetSettings(out var settings))
            {
                return false;
            }

            var assetVaultSetupSettings = AssetVaultSetupSettings.GetOrCreate();
            var localFolderPath = assetVaultSetupSettings.LocalFolderPath;
            if (localFolderPath == null)
            {
                // Local は必須。未設定なら構成を一切変更せず中断する。
                Debug.LogError(LocalFolderMissingMessage);
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

            var duplicateAddressCollector = new AssetVaultDuplicateAddressCollector();
            var registeredGuids = new HashSet<string>();

            // 大量アセットでのインポート再評価を抑えるためバッチ化する。
            AssetDatabase.StartAssetEditing();
            try
            {
                SyncCategory(settings, localFolderPath, true, duplicateAddressCollector, registeredGuids);

                // Remote は任意。設定されている場合のみ同期する。
                var remoteFolderPath = assetVaultSetupSettings.RemoteFolderPath;
                if (remoteFolderPath != null)
                {
                    SyncCategory(settings, remoteFolderPath, false, duplicateAddressCollector, registeredGuids);
                }

                PruneStaleEntries(settings, registeredGuids);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            // アドレス衝突は実行時ロードを壊すため、適用はしても結果は失敗扱いにして再修正を促す。
            if (duplicateAddressCollector.HasDuplicates)
            {
                Debug.LogError(string.Format(DuplicateAddressFailureMessage, duplicateAddressCollector.BuildReport()));
                return false;
            }

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
            var localFolderPath = string.Empty;
            var remoteFolderPath = string.Empty;
            if (AssetVaultSetupSettings.TryLoad(out var setupSettings))
            {
                localFolderPath = setupSettings.LocalFolderPath ?? string.Empty;
                remoteFolderPath = setupSettings.RemoteFolderPath ?? string.Empty;
            }

            if (!AddressableSettingsAccessor.TryGetSettingsSilently(out var settings))
            {
                return new AssetVaultStatus(false, string.Empty, 0, 0, localFolderPath, remoteFolderPath);
            }

            var remoteLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, RemoteLoadPathVariableName);
            var localGroupCount = settings.groups.Count(group => group != null && group.Name.StartsWith(AssetVaultAddressing.LocalGroupPrefix, System.StringComparison.Ordinal));
            var remoteGroupCount = settings.groups.Count(group => group != null && group.Name.StartsWith(AssetVaultAddressing.RemoteGroupPrefix, System.StringComparison.Ordinal));

            return new AssetVaultStatus(
                true,
                remoteLoadPath ?? string.Empty,
                localGroupCount,
                remoteGroupCount,
                localFolderPath,
                remoteFolderPath);
        }

        // --- Internal helpers ---

        private static void SyncCategory(
            AddressableAssetSettings settings,
            string categoryRoot,
            bool isLocal,
            AssetVaultDuplicateAddressCollector duplicateAddressCollector,
            HashSet<string> registeredGuids)
        {
            if (!AssetDatabase.IsValidFolder(categoryRoot))
            {
                Debug.Log(string.Format(CategorySkippedMessageFormat, categoryRoot));
                return;
            }

            var subFolders = AssetDatabase.GetSubFolders(categoryRoot);
            foreach (var subFolder in subFolders)
            {
                var groupName = AssetVaultAddressing.GetGroupName(subFolder, isLocal);
                var group = EnsureGroup(settings, groupName, isLocal);
                RegisterFolder(settings, group, subFolder, categoryRoot, duplicateAddressCollector, registeredGuids);
            }

            RegisterDirectAssets(settings, categoryRoot, isLocal, duplicateAddressCollector, registeredGuids);
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName, bool isLocal)
        {
            var group = settings.FindGroup(groupName);
            var created = false;
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
                created = true;
            }

            ConfigureBundledAssetGroupSchema(settings, group, isLocal, created);
            ConfigureContentUpdateGroupSchema(group, isLocal);
            return group;
        }

        private static void RegisterFolder(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string folder,
            string categoryRoot,
            AssetVaultDuplicateAddressCollector duplicateAddressCollector,
            HashSet<string> registeredGuids)
        {
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            foreach (var guid in guids)
            {
                RegisterAsset(settings, group, guid, categoryRoot, duplicateAddressCollector, registeredGuids);
            }
        }

        private static void RegisterDirectAssets(
            AddressableAssetSettings settings,
            string categoryRoot,
            bool isLocal,
            AssetVaultDuplicateAddressCollector duplicateAddressCollector,
            HashSet<string> registeredGuids)
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

                var directoryPath = AssetVaultAddressing.NormalizeAssetPath(Path.GetDirectoryName(assetPath));
                if (directoryPath != categoryRoot)
                {
                    continue;
                }

                if (group == null)
                {
                    // ルートフォルダ直下のアセットは、そのフォルダ名から作る既定グループにまとめる。
                    group = EnsureGroup(settings, AssetVaultAddressing.GetGroupName(categoryRoot, isLocal), isLocal);
                }

                RegisterAsset(settings, group, guid, categoryRoot, duplicateAddressCollector, registeredGuids);
            }
        }

        private static void RegisterAsset(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string guid,
            string categoryRoot,
            AssetVaultDuplicateAddressCollector duplicateAddressCollector,
            HashSet<string> registeredGuids)
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

            entry.address = AssetVaultAddressing.CreateAddress(assetPath, categoryRoot);
            registeredGuids.Add(guid);
            duplicateAddressCollector.Record(entry.address, assetPath);
        }

        // 管理グループ（Local_/Remote_）から、今回の同期で登録されなかった古いエントリを除去し、空になったグループを削除する。
        private static void PruneStaleEntries(AddressableAssetSettings settings, HashSet<string> registeredGuids)
        {
            var managedGroups = settings.groups
                .Where(group => group != null && AssetVaultAddressing.IsManagedGroupName(group.Name))
                .ToList();

            foreach (var group in managedGroups)
            {
                var staleEntries = group.entries
                    .Where(entry => entry != null && !registeredGuids.Contains(entry.guid))
                    .ToList();
                foreach (var staleEntry in staleEntries)
                {
                    settings.RemoveAssetEntry(staleEntry.guid, false);
                }

                if (group.entries.Count == 0)
                {
                    settings.RemoveGroup(group);
                }
            }
        }

        private static void ConfigureBundledAssetGroupSchema(AddressableAssetSettings settings, AddressableAssetGroup group, bool isLocal, bool created)
        {
            var bundledAssetGroupSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundledAssetGroupSchema == null)
            {
                bundledAssetGroupSchema = group.AddSchema<BundledAssetGroupSchema>();
                created = true;
            }

            // Build/Load パスは Local/Remote の振り分けに直結するため毎回強制する。
            var buildPathVariableName = isLocal ? LocalBuildPathVariableName : RemoteBuildPathVariableName;
            var loadPathVariableName = isLocal ? LocalLoadPathVariableName : RemoteLoadPathVariableName;
            if (!bundledAssetGroupSchema.BuildPath.SetVariableByName(settings, buildPathVariableName))
            {
                Debug.LogWarning(string.Format(ProfileVariableMissingMessageFormat, buildPathVariableName));
            }

            if (!bundledAssetGroupSchema.LoadPath.SetVariableByName(settings, loadPathVariableName))
            {
                Debug.LogWarning(string.Format(ProfileVariableMissingMessageFormat, loadPathVariableName));
            }

            // BundleNaming は新規グループのみ既定値を設定し、既存グループの手動調整は尊重する。
            if (created)
            {
                bundledAssetGroupSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
            }
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
    }
}
