using NUnit.Framework;
using UniLab.AssetVault.Editor;

namespace UniLab.Tests.EditMode.AssetVault
{
    /// <summary>
    /// <see cref="AssetVaultViolation"/> の重大度判定（IsError）の単体テストです。
    /// この判定は Dashboard 表示色とビルド前ゲートの中断可否を兼ねるため、種別ごとに固定して回帰を防ぎます。
    /// </summary>
    public class AssetVaultViolationTest
    {
        /// <summary>
        /// 重複アドレスは実行時ロードを壊す致命傷のため Error（IsError = true）であることを検証します。
        /// </summary>
        [Test]
        public void IsError_DuplicateAddress_IsTrue()
        {
            var violation = new AssetVaultViolation(AssetVaultViolationType.DuplicateAddress, "message");

            Assert.IsTrue(violation.IsError);
        }

        /// <summary>
        /// 孤立ラベルは是正推奨だがビルドは止めない Warning（IsError = false）であることを検証します。
        /// </summary>
        [Test]
        public void IsError_OrphanLabel_IsFalse()
        {
            var violation = new AssetVaultViolation(AssetVaultViolationType.OrphanLabel, "message");

            Assert.IsFalse(violation.IsError);
        }

        /// <summary>
        /// 依存アセットのエントリ化も Warning（IsError = false）であることを検証します。
        /// </summary>
        [Test]
        public void IsError_DependencyRegisteredAsEntry_IsFalse()
        {
            var violation = new AssetVaultViolation(AssetVaultViolationType.DependencyRegisteredAsEntry, "message");

            Assert.IsFalse(violation.IsError);
        }
    }
}
