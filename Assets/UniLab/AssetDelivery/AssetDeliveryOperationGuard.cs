using System;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniLab.AssetDelivery
{
    /// <summary>
    /// Converts Addressables operation failures into asset delivery exceptions.
    /// </summary>
    internal static class AssetDeliveryOperationGuard
    {
        /// <summary>
        /// Throws an asset delivery exception when an Addressables operation handle failed.
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
        /// Converts an exception into an asset delivery exception while preserving existing asset delivery exceptions.
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
