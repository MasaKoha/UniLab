using NUnit.Framework;
using UniLab.AssetVault;

namespace UniLab.Tests.EditMode.AssetVault
{
    /// <summary>
    /// ContentVersionInfo の単体テストです。
    /// </summary>
    public class ContentVersionInfoTest
    {
        /// <summary>
        /// コンストラクタ引数が各プロパティに保持されることを検証します。
        /// </summary>
        [Test]
        public void Constructor_SetsContentVersionAndPath()
        {
            var contentVersionInfo = new ContentVersionInfo("00052", "01J9Z8K3Q4XR");

            Assert.AreEqual("00052", contentVersionInfo.ContentVersion);
            Assert.AreEqual("01J9Z8K3Q4XR", contentVersionInfo.Path);
        }
    }
}
