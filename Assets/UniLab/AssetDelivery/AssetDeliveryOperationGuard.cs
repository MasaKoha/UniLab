using System;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniLab.AssetDelivery
{
    /// <summary>
    /// Addressables 操作の失敗を asset delivery 例外に変換します。
    /// </summary>
    internal static class AssetDeliveryOperationGuard
    {
        /// <summary>
        /// Addressables operation handle が失敗している場合に asset delivery 例外を送出します。
        /// </summary>
        public static void ThrowIfFailed(AsyncOperationHandle handle, string message)
        {
            if (handle.Status != AsyncOperationStatus.Failed)
            {
                return;
            }

            var exception = handle.OperationException ?? new InvalidOperationException(message);
            throw new AssetDeliveryException(message, exception);
        }

        /// <summary>
        /// 既存の asset delivery 例外を保持しつつ、例外を asset delivery 例外に変換します。
        /// </summary>
        public static AssetDeliveryException ToAssetDeliveryException(Exception exception, string message)
        {
            if (exception is AssetDeliveryException assetDeliveryException)
            {
                return assetDeliveryException;
            }

            return new AssetDeliveryException(message, exception);
        }
    }
}
