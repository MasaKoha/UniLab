using System.Collections.Generic;
using NUnit.Framework;
using UniLab.AssetVault.Editor;

namespace UniLab.Tests.EditMode.AssetVault
{
    /// <summary>
    /// 規約違反判定の純粋ロジック <see cref="AssetVaultConventionRules"/> の単体テストです。
    /// Addressables 設定・AssetDatabase に依存しない収集済みデータだけで検証します。
    /// </summary>
    public class AssetVaultConventionRulesTest
    {
        /// <summary>
        /// 同一アドレスが複数エントリに付いている場合、DuplicateAddress 違反を1件だけ追加し、対象パスを含むことを検証します。
        /// </summary>
        [Test]
        public void AddDuplicateAddressViolations_DuplicateAddress_AddsSingleViolationWithPaths()
        {
            var managedEntries = new List<AssetVaultConventionRules.ManagedEntry>
            {
                new AssetVaultConventionRules.ManagedEntry("Icons/coin", "Assets/Local/UI/coin.png"),
                new AssetVaultConventionRules.ManagedEntry("Icons/coin", "Assets/Local/UI/coin_dup.png"),
                new AssetVaultConventionRules.ManagedEntry("Icons/gem", "Assets/Local/UI/gem.png"),
            };
            var violations = new List<AssetVaultViolation>();

            AssetVaultConventionRules.AddDuplicateAddressViolations(managedEntries, violations);

            Assert.AreEqual(1, violations.Count);
            Assert.AreEqual(AssetVaultViolationType.DuplicateAddress, violations[0].ViolationType);
            Assert.That(violations[0].Message, Does.Contain("Icons/coin"));
            Assert.That(violations[0].Message, Does.Contain("coin.png"));
            Assert.That(violations[0].Message, Does.Contain("coin_dup.png"));
        }

        /// <summary>
        /// アドレスがすべて一意なら違反を追加しないことを検証します。
        /// </summary>
        [Test]
        public void AddDuplicateAddressViolations_AllUnique_AddsNothing()
        {
            var managedEntries = new List<AssetVaultConventionRules.ManagedEntry>
            {
                new AssetVaultConventionRules.ManagedEntry("Icons/coin", "Assets/Local/UI/coin.png"),
                new AssetVaultConventionRules.ManagedEntry("Icons/gem", "Assets/Local/UI/gem.png"),
            };
            var violations = new List<AssetVaultViolation>();

            AssetVaultConventionRules.AddDuplicateAddressViolations(managedEntries, violations);

            Assert.IsEmpty(violations);
        }

        /// <summary>
        /// どのエントリも使っていないラベルだけ OrphanLabel 違反になることを検証します。
        /// </summary>
        [Test]
        public void AddOrphanLabelViolations_UnusedLabels_AddsViolationsForUnusedOnly()
        {
            var allLabels = new[] { "Icons", "Characters", "Stage" };
            var usedLabels = new HashSet<string> { "Icons" };
            var violations = new List<AssetVaultViolation>();

            AssetVaultConventionRules.AddOrphanLabelViolations(allLabels, usedLabels, violations);

            Assert.AreEqual(2, violations.Count);
            Assert.IsTrue(violations.TrueForAll(violation => violation.ViolationType == AssetVaultViolationType.OrphanLabel));
            Assert.That(violations[0].Message, Does.Contain("Characters"));
            Assert.That(violations[1].Message, Does.Contain("Stage"));
        }

        /// <summary>
        /// すべてのラベルがいずれかのエントリに使われていれば違反を追加しないことを検証します。
        /// </summary>
        [Test]
        public void AddOrphanLabelViolations_AllUsed_AddsNothing()
        {
            var allLabels = new[] { "Icons", "Characters" };
            var usedLabels = new HashSet<string> { "Icons", "Characters" };
            var violations = new List<AssetVaultViolation>();

            AssetVaultConventionRules.AddOrphanLabelViolations(allLabels, usedLabels, violations);

            Assert.IsEmpty(violations);
        }

        /// <summary>
        /// 別の管理エントリの直接依存にもなっている管理アセットが抽出されることを検証します。
        /// </summary>
        [Test]
        public void CollectEntriesReferencedAsDependency_DependencyIsManaged_ReturnsThatPath()
        {
            var entriesWithDependencies = new List<(string assetPath, IReadOnlyList<string> directDependencies)>
            {
                ("Assets/Local/Characters/hero.prefab", new[] { "Assets/Local/Shared/body.mat" }),
                ("Assets/Local/Shared/body.mat", new string[0]),
            };
            var managedPaths = new HashSet<string>
            {
                "Assets/Local/Characters/hero.prefab",
                "Assets/Local/Shared/body.mat",
            };

            var referenced = AssetVaultConventionRules.CollectEntriesReferencedAsDependency(entriesWithDependencies, managedPaths);

            Assert.AreEqual(1, referenced.Count);
            Assert.IsTrue(referenced.Contains("Assets/Local/Shared/body.mat"));
        }

        /// <summary>
        /// 依存先が管理対象外（未登録の依存）なら抽出しないことを検証します。
        /// </summary>
        [Test]
        public void CollectEntriesReferencedAsDependency_DependencyNotManaged_ReturnsEmpty()
        {
            var entriesWithDependencies = new List<(string assetPath, IReadOnlyList<string> directDependencies)>
            {
                ("Assets/Local/Characters/hero.prefab", new[] { "Assets/Local/Characters/_src/hero.anim" }),
            };
            var managedPaths = new HashSet<string> { "Assets/Local/Characters/hero.prefab" };

            var referenced = AssetVaultConventionRules.CollectEntriesReferencedAsDependency(entriesWithDependencies, managedPaths);

            Assert.IsEmpty(referenced);
        }

        /// <summary>
        /// 自分自身を依存に含む（GetDependencies が自パスを返す）ケースを除外することを検証します。
        /// </summary>
        [Test]
        public void CollectEntriesReferencedAsDependency_SelfDependency_Ignored()
        {
            var entriesWithDependencies = new List<(string assetPath, IReadOnlyList<string> directDependencies)>
            {
                ("Assets/Local/Characters/hero.prefab", new[] { "Assets/Local/Characters/hero.prefab" }),
            };
            var managedPaths = new HashSet<string> { "Assets/Local/Characters/hero.prefab" };

            var referenced = AssetVaultConventionRules.CollectEntriesReferencedAsDependency(entriesWithDependencies, managedPaths);

            Assert.IsEmpty(referenced);
        }

        /// <summary>
        /// 抽出済みの依存パスごとに DependencyRegisteredAsEntry 違反を追加することを検証します。
        /// </summary>
        [Test]
        public void AddDependencyEntryViolations_AddsViolationPerPath()
        {
            var referencedAsDependency = new HashSet<string>
            {
                "Assets/Local/Shared/body.mat",
            };
            var violations = new List<AssetVaultViolation>();

            AssetVaultConventionRules.AddDependencyEntryViolations(referencedAsDependency, violations);

            Assert.AreEqual(1, violations.Count);
            Assert.AreEqual(AssetVaultViolationType.DependencyRegisteredAsEntry, violations[0].ViolationType);
            Assert.That(violations[0].Message, Does.Contain("body.mat"));
        }
    }
}
