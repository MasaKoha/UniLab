using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault 管理グループへの Addressables 登録処理を、手動 Sync と自動差分処理で共有します。
    /// </summary>
    internal static class AssetVaultGroupRegistrar
    {
        /// <summary>Local グループの BuildPath に使う Addressables プロファイル変数名です。</summary>
        internal const string LocalBuildPathVariableName = "LocalBuildPath";

        /// <summary>Local グループの LoadPath に使う Addressables プロファイル変数名です。</summary>
        internal const string LocalLoadPathVariableName = "LocalLoadPath";

        /// <summary>Remote グループの BuildPath に使う Addressables プロファイル変数名です。</summary>
        internal const string RemoteBuildPathVariableName = "RemoteBuildPath";

        /// <summary>Remote グループの LoadPath に使う Addressables プロファイル変数名です。</summary>
        internal const string RemoteLoadPathVariableName = "RemoteLoadPath";

        private const string LocalBuildPathValue = "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]";
        private const string LocalLoadPathValue = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]";
        private const string RemoteBuildPathValue = "ServerData/[BuildTarget]";
        private const string BuildTargetToken = "[BuildTarget]";
        private const string BaseUrlPropertyName = "BaseUrl";
        private const string ContentPathPropertyName = "ContentPath";
        private const string ProfileVariableMissingMessageFormat = "Addressables profile variable '{0}' was not found; group build/load path may be misconfigured.";

        /// <summary>
        /// AssetVault 管理グループのスキーマが参照する Addressables プロファイル変数を用意します。
        /// </summary>
        internal static void EnsureProfileValues(AddressableAssetSettings settings)
        {
            EnsureProfileValue(settings, LocalBuildPathVariableName, LocalBuildPathValue);
            EnsureProfileValue(settings, LocalLoadPathVariableName, LocalLoadPathValue);
            EnsureProfileValue(settings, RemoteBuildPathVariableName, RemoteBuildPathValue);
            EnsureProfileValue(settings, RemoteLoadPathVariableName, CreateRemoteLoadPath());

            // Remote は AssetVaultRuntime トークンと ServerData 規約に毎回追従させる必要がある。
            settings.profileSettings.SetValue(settings.activeProfileId, RemoteLoadPathVariableName, CreateRemoteLoadPath());
            settings.profileSettings.SetValue(settings.activeProfileId, RemoteBuildPathVariableName, RemoteBuildPathValue);
        }

        /// <summary>
        /// 指定名の Addressables グループを取得または作成し、AssetVault の Local/Remote スキーマ設定へ揃えます。
        /// </summary>
        internal static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName, bool isLocal)
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

        /// <summary>
        /// guid のアセットを指定グループへ登録し、手動 Sync と同じアドレス・ラベル規則を適用します。
        /// </summary>
        internal static void RegisterAsset(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string guid,
            string categoryRoot,
            string label,
            AssetVaultDuplicateAddressCollector duplicateAddressCollector = null,
            HashSet<string> registeredGuids = null)
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
            // 既存ラベルを一旦すべて外し、カテゴリフォルダ由来のラベル1つだけにする。
            // 別カテゴリへ移動したとき旧フォルダのラベルが残るのを防ぐ（auto/Sync 共通。ラベルはフォルダ規約で自動付与する設計のため手動ラベルは想定しない）。
            ClearLabels(entry);
            // フォルダ単位の一括ロード（LoadAssetsAsync<T>(label)）用にラベルを付与する。
            // force:true で settings 未登録のラベルは自動登録し、postEvent:false でバッチ中の再評価を抑える。
            entry.SetLabel(label, true, true, false);
            registeredGuids?.Add(guid);
            duplicateAddressCollector?.Record(entry.address, assetPath);
        }

        /// <summary>
        /// 単一アセットを現在のパスからカテゴリ解決し、対応する AssetVault 管理グループへ差分登録します。
        /// </summary>
        internal static void RegisterSingle(
            AddressableAssetSettings settings,
            string assetPath,
            string categoryRoot,
            bool isLocal,
            IDictionary<string, AddressableAssetGroup> groupCache = null)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            // 自動側では重複アドレスの厳密検出は行わない（全走査は大量インポートで重いため）。衝突検出は手動 Sync の責務。
            var categoryFolder = AssetVaultAddressing.ResolveCategoryFolder(assetPath, categoryRoot);
            var groupName = AssetVaultAddressing.GetGroupName(categoryFolder, isLocal);
            var label = AssetVaultAddressing.CreateLabel(categoryFolder);
            var group = EnsureGroupCached(settings, groupName, isLocal, groupCache);
            RegisterAsset(settings, group, guid, categoryRoot, label);
        }

        /// <summary>
        /// フォルダ配下の全アセットパスを列挙します（フォルダ自身は除外）。フォルダ移動/削除の差分処理で配下を辿るのに使います。
        /// </summary>
        internal static IEnumerable<string> EnumerateFolderAssetPaths(string folderPath)
        {
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                yield return assetPath;
            }
        }

        // バッチ内で同一グループへの EnsureGroup（スキーマ再設定）が何度も走るのを防ぐため、解決済みグループをキャッシュする。
        private static AddressableAssetGroup EnsureGroupCached(
            AddressableAssetSettings settings,
            string groupName,
            bool isLocal,
            IDictionary<string, AddressableAssetGroup> groupCache)
        {
            if (groupCache != null && groupCache.TryGetValue(groupName, out var cachedGroup))
            {
                return cachedGroup;
            }

            var group = EnsureGroup(settings, groupName, isLocal);
            if (groupCache != null)
            {
                groupCache[groupName] = group;
            }

            return group;
        }

        /// <summary>
        /// guid の Addressables エントリを AssetVault 管理対象から除去します。存在しない場合は何もしません。
        /// </summary>
        internal static void RemoveEntry(AddressableAssetSettings settings, string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            settings.RemoveAssetEntry(guid, false);
        }

        /// <summary>
        /// 今回の手動 Sync で登録されなかった古いエントリを管理グループから除去し、空グループを削除します。
        /// </summary>
        internal static void PruneStaleEntries(AddressableAssetSettings settings, HashSet<string> registeredGuids)
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

                RemoveGroupIfEmpty(settings, group);
            }
        }

        /// <summary>
        /// 空になった AssetVault 管理グループを削除します。
        /// </summary>
        internal static void PruneEmptyManagedGroups(AddressableAssetSettings settings)
        {
            var managedGroups = settings.groups
                .Where(group => group != null && AssetVaultAddressing.IsManagedGroupName(group.Name))
                .ToList();

            foreach (var group in managedGroups)
            {
                RemoveGroupIfEmpty(settings, group);
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

        // エントリに付いている既存ラベルをすべて外す。force:false（settings からは消さない）・postEvent:false でバッチ中の再評価を抑える。
        private static void ClearLabels(AddressableAssetEntry entry)
        {
            foreach (var existingLabel in entry.labels.ToList())
            {
                entry.SetLabel(existingLabel, false, false, false);
            }
        }

        private static void RemoveGroupIfEmpty(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            if (group.entries.Count > 0)
            {
                return;
            }

            settings.RemoveGroup(group);
        }
    }
}
