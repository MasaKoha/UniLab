using NUnit.Framework;
using UniLab.AssetVault;

namespace UniLab.Tests.EditMode.AssetVault
{
    /// <summary>
    /// AssetVaultRuntime の単体テストです。
    /// </summary>
    public class AssetVaultRuntimeTest
    {
        /// <summary>
        /// 各テスト前に静的状態を既定値へ戻します。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            ResetRuntime();
        }

        /// <summary>
        /// 各テスト後に静的状態を既定値へ戻します。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            ResetRuntime();
        }

        /// <summary>
        /// 既定の基底 URL が null であることを検証します。
        /// </summary>
        [Test]
        public void BaseUrl_DefaultValue_IsNull()
        {
            Assert.IsNull(AssetVaultRuntime.BaseUrl);
        }

        /// <summary>
        /// BaseUrl の set と get が反映されることを検証します。
        /// </summary>
        [Test]
        public void BaseUrl_SetValue_ReturnsAssignedValue()
        {
            AssetVaultRuntime.BaseUrl = "https://dev1.xxx.xxx/app";

            Assert.AreEqual("https://dev1.xxx.xxx/app", AssetVaultRuntime.BaseUrl);
        }

        /// <summary>
        /// ContentPath の set と get が反映されることを検証します。
        /// </summary>
        [Test]
        public void ContentPath_SetValue_ReturnsAssignedValue()
        {
            AssetVaultRuntime.ContentPath = "01J9Z8K3Q4XR";

            Assert.AreEqual("01J9Z8K3Q4XR", AssetVaultRuntime.ContentPath);
        }

        private static void ResetRuntime()
        {
            AssetVaultRuntime.BaseUrl = null;
            AssetVaultRuntime.ContentPath = null;
        }
    }
}
