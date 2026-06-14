using NUnit.Framework;
using UniLab.AssetVault.Editor;

namespace UniLab.AssetVault.Editor.Tests
{
    /// <summary>
    /// <see cref="AssetVaultDuplicateAddressCollector"/> の重複検出ロジックを検証します。
    /// </summary>
    public sealed class AssetVaultDuplicateAddressCollectorTest
    {
        [Test]
        public void アドレスが異なれば重複ではない()
        {
            var collector = new AssetVaultDuplicateAddressCollector();
            collector.Record("a", "Assets/a.png");
            collector.Record("b", "Assets/b.png");
            Assert.IsFalse(collector.HasDuplicates);
        }

        [Test]
        public void 同一アドレスでも同一アセットなら重複ではない()
        {
            var collector = new AssetVaultDuplicateAddressCollector();
            collector.Record("a", "Assets/a.png");
            collector.Record("a", "Assets/a.png");
            Assert.IsFalse(collector.HasDuplicates);
        }

        [Test]
        public void 同一アドレスで別アセットなら重複として検出しレポートに含む()
        {
            var collector = new AssetVaultDuplicateAddressCollector();
            collector.Record("hero", "Assets/x.png");
            collector.Record("hero", "Assets/y.png");
            Assert.IsTrue(collector.HasDuplicates);
            StringAssert.Contains("hero", collector.BuildReport());
        }
    }
}
