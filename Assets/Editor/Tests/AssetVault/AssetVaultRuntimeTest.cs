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
        /// 既定の環境名が prod であることを検証します。
        /// </summary>
        [Test]
        public void Environment_DefaultValue_IsProd()
        {
            Assert.AreEqual("prod", AssetVaultRuntime.Environment);
        }

        /// <summary>
        /// Environment の set と get が反映されることを検証します。
        /// </summary>
        [Test]
        public void Environment_SetValue_ReturnsAssignedValue()
        {
            AssetVaultRuntime.Environment = "staging";

            Assert.AreEqual("staging", AssetVaultRuntime.Environment);
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
            AssetVaultRuntime.Environment = "prod";
            AssetVaultRuntime.ContentPath = null;
        }
    }
}
