using System.Collections.Generic;
using System.Linq;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// 規約違反の判定ロジック本体（純粋関数）。Addressables 設定・AssetDatabase に依存しないため EditMode で単体テストできます。
    /// 設定からのデータ収集は <see cref="AssetVaultConventionChecker"/> が担い、ここは収集済みデータから違反を導出することだけに専念します。
    /// </summary>
    internal static class AssetVaultConventionRules
    {
        /// <summary>
        /// 規約判定に必要な、管理エントリ1件分の最小データです（Addressables 型に依存させないための受け皿）。
        /// </summary>
        internal readonly struct ManagedEntry
        {
            /// <summary>アドレスとアセットパスを指定して生成します。</summary>
            public ManagedEntry(string address, string assetPath)
            {
                Address = address;
                AssetPath = assetPath;
            }

            /// <summary>Addressables アドレス。</summary>
            public string Address { get; }

            /// <summary>アセットパス。</summary>
            public string AssetPath { get; }
        }

        /// <summary>
        /// 同一アドレスが2件以上の管理エントリに付いている重複を違反として <paramref name="violations"/> に追加します。
        /// 同一アドレスは実行時ロードを壊すため Error 相当の違反です。
        /// </summary>
        internal static void AddDuplicateAddressViolations(IReadOnlyList<ManagedEntry> managedEntries, List<AssetVaultViolation> violations)
        {
            var duplicateGroups = managedEntries
                .GroupBy(entry => entry.Address)
                .Where(group => group.Count() > 1);

            foreach (var duplicateGroup in duplicateGroups)
            {
                var paths = string.Join(", ", duplicateGroup.Select(entry => entry.AssetPath));
                violations.Add(new AssetVaultViolation(
                    AssetVaultViolationType.DuplicateAddress,
                    $"Duplicate address '{duplicateGroup.Key}': {paths}"));
            }
        }

        /// <summary>
        /// どのエントリも使っていない孤立ラベル（auto 登録の入れ替えで残った等）を違反として追加します。
        /// </summary>
        internal static void AddOrphanLabelViolations(IEnumerable<string> allLabels, ICollection<string> usedLabels, List<AssetVaultViolation> violations)
        {
            foreach (var label in allLabels)
            {
                if (usedLabels.Contains(label))
                {
                    continue;
                }

                violations.Add(new AssetVaultViolation(
                    AssetVaultViolationType.OrphanLabel,
                    $"Orphan label '{label}' (not used by any entry)"));
            }
        }

        /// <summary>
        /// 各管理エントリの直接依存集合から、別の管理エントリの依存にもなっている管理アセットのパスを抽出します。
        /// （= 起点としても依存としても使われているアセット。重複バンドルの温床。）
        /// 直接依存のみを見る前提で、間接依存は呼び出し側で渡さないこと（親が同梱するため対象外）。
        /// </summary>
        internal static HashSet<string> CollectEntriesReferencedAsDependency(
            IReadOnlyList<(string assetPath, IReadOnlyList<string> directDependencies)> entriesWithDependencies,
            ISet<string> managedPaths)
        {
            var referencedAsDependency = new HashSet<string>();
            foreach (var entry in entriesWithDependencies)
            {
                foreach (var dependency in entry.directDependencies)
                {
                    if (dependency != entry.assetPath && managedPaths.Contains(dependency))
                    {
                        referencedAsDependency.Add(dependency);
                    }
                }
            }

            return referencedAsDependency;
        }

        /// <summary>
        /// 依存としても参照されている管理アセット群を違反として追加します。
        /// 単一利用なら "_" skip フォルダへ、共有なら共有グループへ寄せると重複バンドルを防げます。
        /// </summary>
        internal static void AddDependencyEntryViolations(IReadOnlyCollection<string> referencedAsDependency, List<AssetVaultViolation> violations)
        {
            foreach (var path in referencedAsDependency)
            {
                violations.Add(new AssetVaultViolation(
                    AssetVaultViolationType.DependencyRegisteredAsEntry,
                    $"Dependency asset is registered as an entry (consider a '_' skip folder or a shared group): {path}"));
            }
        }
    }
}
