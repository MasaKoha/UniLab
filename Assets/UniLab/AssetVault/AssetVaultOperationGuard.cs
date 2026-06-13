using System;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniLab.AssetVault
{
    /// <summary>
    /// Addressables 操作の失敗を asset vault 例外に変換します。
    /// </summary>
    internal static class AssetVaultOperationGuard
    {
        /// <summary>
        /// Addressables operation handle が失敗している場合に asset vault 例外を送出します。
        /// </summary>
        public static void ThrowIfFailed(AsyncOperationHandle handle, string message)
        {
            if (handle.Status != AsyncOperationStatus.Failed)
            {
                return;
            }

            var exception = handle.OperationException ?? new InvalidOperationException(message);
            throw new AssetVaultException(message, exception);
        }

        /// <summary>
        /// 既存の asset vault 例外を保持しつつ、例外を asset vault 例外に変換します。
        /// </summary>
        public static AssetVaultException ToAssetVaultException(Exception exception, string message)
        {
            if (exception is AssetVaultException assetVaultException)
            {
                return assetVaultException;
            }

            return new AssetVaultException(message, exception);
        }
    }
}
