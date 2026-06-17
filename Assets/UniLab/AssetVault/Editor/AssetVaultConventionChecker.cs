using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault 管理グループ（Local_/Remote_）の規約違反を Addressables 設定から検出します。
    /// 設定・AssetDatabase からのデータ収集をここで担い、違反判定そのものは純粋ロジックの <see cref="AssetVaultConventionRules"/> に委譲します（テスト容易性のための分離）。
    /// 検出対象: 重複アドレス・孤立ラベル・依存アセットのエントリ化（skip 漏れ候補）。
    /// </summary>
    internal static class AssetVaultConventionChecker
    {
        /// <summary>
        /// 管理グループ全体を走査して規約違反を一覧で返します。違反が無ければ空リストです。
        /// </summary>
        internal static IReadOnlyList<AssetVaultViolation> Check(AddressableAssetSettings settings)
        {
            var violations = new List<AssetVaultViolation>();
            var managedEntries = CollectManagedEntries(settings);

            var entryInfos = managedEntries
                .Select(entry => new AssetVaultConventionRules.ManagedEntry(entry.address, entry.AssetPath))
                .ToList();
            AssetVaultConventionRules.AddDuplicateAddressViolations(entryInfos, violations);

            AssetVaultConventionRules.AddOrphanLabelViolations(settings.GetLabels(), CollectUsedLabels(settings), violations);

            var managedPaths = new HashSet<string>(managedEntries.Select(entry => entry.AssetPath));
            // 直接依存のみを見る（recursive=false）。間接依存は親が同梱するため対象外。GetDependencies は AssetDatabase 依存なのでここで収集する。
            var entriesWithDependencies = managedEntries
                .Select(entry => (entry.AssetPath, (IReadOnlyList<string>)AssetDatabase.GetDependencies(entry.AssetPath, false)))
                .ToList();
            var referencedAsDependency = AssetVaultConventionRules.CollectEntriesReferencedAsDependency(entriesWithDependencies, managedPaths);
            AssetVaultConventionRules.AddDependencyEntryViolations(referencedAsDependency, violations);

            return violations;
        }

        private static List<AddressableAssetEntry> CollectManagedEntries(AddressableAssetSettings settings)
        {
            var entries = new List<AddressableAssetEntry>();
            foreach (var group in settings.groups)
            {
                if (group == null || !AssetVaultAddressing.IsManagedGroupName(group.Name))
                {
                    continue;
                }

                entries.AddRange(group.entries.Where(entry => entry != null));
            }

            return entries;
        }

        private static HashSet<string> CollectUsedLabels(AddressableAssetSettings settings)
        {
            var usedLabels = new HashSet<string>();
            foreach (var group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (var entry in group.entries)
                {
                    if (entry != null)
                    {
                        usedLabels.UnionWith(entry.labels);
                    }
                }
            }

            return usedLabels;
        }
    }
}
