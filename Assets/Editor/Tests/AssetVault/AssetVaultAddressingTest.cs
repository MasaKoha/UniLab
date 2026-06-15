using NUnit.Framework;
using UniLab.AssetVault.Editor;

namespace UniLab.AssetVault.Editor.Tests
{
    /// <summary>
    /// <see cref="AssetVaultAddressing"/> の純粋ロジック（アドレス・グループ名・正規化）の振る舞いを検証します。
    /// </summary>
    public sealed class AssetVaultAddressingTest
    {
        [Test]
        public void CreateAddress_カテゴリルート相対かつ拡張子なしになる()
        {
            var address = AssetVaultAddressing.CreateAddress("Assets/Remote/Characters/hero.prefab", "Assets/Remote");
            Assert.AreEqual("Characters/hero", address);
        }

        [Test]
        public void CreateAddress_拡張子が無ければそのまま相対パスを返す()
        {
            var address = AssetVaultAddressing.CreateAddress("Assets/Remote/Folder/data", "Assets/Remote");
            Assert.AreEqual("Folder/data", address);
        }

        [Test]
        public void GetGroupName_プレフィックスと末尾フォルダ名から作られる()
        {
            Assert.AreEqual("Local_Internal", AssetVaultAddressing.GetGroupName("Assets/Res/Internal", true));
            Assert.AreEqual("Remote_External", AssetVaultAddressing.GetGroupName("Assets/Res/External", false));
        }

        [Test]
        public void CreateLabel_フォルダ名をプレフィックスなしで返す()
        {
            Assert.AreEqual("Icons", AssetVaultAddressing.CreateLabel("Assets/Local/Icons"));
            Assert.AreEqual("Characters", AssetVaultAddressing.CreateLabel("Assets/Remote/Characters"));
        }

        [Test]
        public void NormalizeAssetPath_区切り統一と末尾スラッシュ除去をする()
        {
            Assert.AreEqual("Assets/Foo/Bar", AssetVaultAddressing.NormalizeAssetPath("Assets\\Foo\\Bar/"));
            Assert.AreEqual(string.Empty, AssetVaultAddressing.NormalizeAssetPath(null));
        }

        [Test]
        public void IsUnderRoot_一致と配下だけをtrueにする()
        {
            Assert.IsTrue(AssetVaultAddressing.IsUnderRoot("Assets/Local", "Assets/Local"));
            Assert.IsTrue(AssetVaultAddressing.IsUnderRoot("Assets/Local/Icon.png", "Assets/Local"));
            Assert.IsFalse(AssetVaultAddressing.IsUnderRoot("Assets/Remote/Icon.png", "Assets/Local"));
            Assert.IsFalse(AssetVaultAddressing.IsUnderRoot("Assets/LocalStuff/Icon.png", "Assets/Local"));
        }

        [Test]
        public void ResolveCategoryFolder_ルート直下はルート自身を返す()
        {
            var categoryFolder = AssetVaultAddressing.ResolveCategoryFolder("Assets/Local/Icon.png", "Assets/Local");
            Assert.AreEqual("Assets/Local", categoryFolder);
        }

        [Test]
        public void ResolveCategoryFolder_サブフォルダ直下は第一階層を返す()
        {
            var categoryFolder = AssetVaultAddressing.ResolveCategoryFolder("Assets/Local/Icons/Icon.png", "Assets/Local");
            Assert.AreEqual("Assets/Local/Icons", categoryFolder);
        }

        [Test]
        public void ResolveCategoryFolder_深いネストも第一階層を返す()
        {
            var categoryFolder = AssetVaultAddressing.ResolveCategoryFolder("Assets/Local/Icons/Sub/Icon.png", "Assets/Local");
            Assert.AreEqual("Assets/Local/Icons", categoryFolder);
        }

        [Test]
        public void IsManagedGroupName_LocalRemoteプレフィックスのみ管理対象と判定する()
        {
            Assert.IsTrue(AssetVaultAddressing.IsManagedGroupName("Local_Foo"));
            Assert.IsTrue(AssetVaultAddressing.IsManagedGroupName("Remote_Bar"));
            Assert.IsFalse(AssetVaultAddressing.IsManagedGroupName("Default Local Group"));
            Assert.IsFalse(AssetVaultAddressing.IsManagedGroupName(null));
        }
    }
}
