using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault 管理グループ（Local_/Remote_）の規約違反を Addressables 設定から検出します。
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

            AddDuplicateAddressViolations(managedEntries, violations);
            AddOrphanLabelViolations(settings, violations);
            AddDependencyEntryViolations(managedEntries, violations);
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

        // 同一アドレスが2件以上の管理エントリに付いている場合に違反とする（実行時ロードが壊れるため）。
        private static void AddDuplicateAddressViolations(IReadOnlyList<AddressableAssetEntry> managedEntries, List<AssetVaultViolation> violations)
        {
            var duplicateGroups = managedEntries
                .GroupBy(entry => entry.address)
                .Where(group => group.Count() > 1);

            foreach (var duplicateGroup in duplicateGroups)
            {
                var paths = string.Join(", ", duplicateGroup.Select(entry => entry.AssetPath));
                violations.Add(new AssetVaultViolation(
                    AssetVaultViolationType.DuplicateAddress,
                    $"重複アドレス '{duplicateGroup.Key}': {paths}"));
            }
        }

        // どのエントリも使っていないラベル（auto 登録の入れ替えで残った孤立ラベル等）を違反とする。
        private static void AddOrphanLabelViolations(AddressableAssetSettings settings, List<AssetVaultViolation> violations)
        {
            var usedLabels = CollectUsedLabels(settings);
            foreach (var label in settings.GetLabels())
            {
                if (usedLabels.Contains(label))
                {
                    continue;
                }

                violations.Add(new AssetVaultViolation(
                    AssetVaultViolationType.OrphanLabel,
                    $"孤立ラベル '{label}'（どのエントリも使用していません）"));
            }
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

        // 他の管理エントリの依存でもあるアセットが、自身もエントリ登録されている場合に違反とする。
        // 単一利用なら "_" skip フォルダへ、共有なら共有グループへ寄せると重複バンドルを防げる。
        private static void AddDependencyEntryViolations(IReadOnlyList<AddressableAssetEntry> managedEntries, List<AssetVaultViolation> violations)
        {
            var managedPaths = new HashSet<string>(managedEntries.Select(entry => entry.AssetPath));
            var referencedAsDependency = CollectEntriesReferencedAsDependency(managedEntries, managedPaths);

            foreach (var path in referencedAsDependency)
            {
                violations.Add(new AssetVaultViolation(
                    AssetVaultViolationType.DependencyRegisteredAsEntry,
                    $"依存アセットがエントリ登録されています（'_' skip フォルダ か共有グループ化を検討）: {path}"));
            }
        }

        private static HashSet<string> CollectEntriesReferencedAsDependency(IReadOnlyList<AddressableAssetEntry> managedEntries, HashSet<string> managedPaths)
        {
            var referencedAsDependency = new HashSet<string>();
            foreach (var entry in managedEntries)
            {
                // 直接依存のみを見る（recursive=false）。間接依存は親が同梱するため対象外。
                var dependencies = AssetDatabase.GetDependencies(entry.AssetPath, false);
                foreach (var dependency in dependencies)
                {
                    if (dependency != entry.AssetPath && managedPaths.Contains(dependency))
                    {
                        referencedAsDependency.Add(dependency);
                    }
                }
            }

            return referencedAsDependency;
        }
    }
}
