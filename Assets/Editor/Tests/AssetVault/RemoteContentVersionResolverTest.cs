using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniLab.AssetVault;

namespace UniLab.Tests.EditMode.AssetVault
{
    /// <summary>
    /// RemoteContentVersionResolver の単体テストです。
    /// </summary>
    public class RemoteContentVersionResolverTest
    {
        /// <summary>
        /// 基底 URL 末尾のスラッシュが重複せず version.json の URL が作られることを検証します。
        /// </summary>
        [Test]
        public void ResolveAsync_NormalizesUrl_WhenBaseUrlEndsWithSlash()
        {
            string requestedUrl = null;
            var resolver = new RemoteContentVersionResolver(
                "https://cdn/app/",
                "prod",
                (url, cancellationToken) =>
                {
                    requestedUrl = url;
                    return UniTask.FromResult("{\"contentVersion\":\"00052\",\"path\":\"01J9Z8K3Q4XR\"}");
                });

            resolver.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("https://cdn/app/prod/version.json", requestedUrl);
        }

        /// <summary>
        /// 正常な JSON からコンテンツ版情報を返すことを検証します。
        /// </summary>
        [Test]
        public void ResolveAsync_ReturnsContentVersion_WhenJsonValid()
        {
            var resolver = new RemoteContentVersionResolver(
                "https://cdn/app",
                "prod",
                (url, cancellationToken) => UniTask.FromResult("{\"contentVersion\":\"00052\",\"path\":\"01J9Z8K3Q4XR\"}"));

            var contentVersionInfo = resolver.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("00052", contentVersionInfo.ContentVersion);
            Assert.AreEqual("01J9Z8K3Q4XR", contentVersionInfo.Path);
        }

        /// <summary>
        /// 取得処理の AssetVaultException が二重ラップされないことを検証します。
        /// </summary>
        [Test]
        public void ResolveAsync_ThrowsSameAssetVaultException_WhenFetcherFails()
        {
            var expectedException = new AssetVaultException("failed");
            var resolver = new RemoteContentVersionResolver(
                "https://cdn/app",
                "prod",
                (url, cancellationToken) => throw expectedException);

            var actualException = Assert.Throws<AssetVaultException>(
                () => resolver.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult());

            Assert.AreSame(expectedException, actualException);
        }

        /// <summary>
        /// 不正な JSON が AssetVaultException に変換されることを検証します。
        /// </summary>
        [Test]
        public void ResolveAsync_ThrowsAssetVaultException_WhenJsonInvalid()
        {
            var resolver = new RemoteContentVersionResolver(
                "https://cdn/app",
                "prod",
                (url, cancellationToken) => UniTask.FromResult("not json"));

            Assert.Throws<AssetVaultException>(
                () => resolver.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult());
        }

        /// <summary>
        /// キャンセル例外が AssetVaultException に変換されないことを検証します。
        /// </summary>
        [Test]
        public void ResolveAsync_ThrowsOperationCanceledException_WhenFetcherCanceled()
        {
            var resolver = new RemoteContentVersionResolver(
                "https://cdn/app",
                "prod",
                (url, cancellationToken) => throw new OperationCanceledException());

            Assert.Throws<OperationCanceledException>(
                () => resolver.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult());
        }
    }
}
