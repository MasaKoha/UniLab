using System;

namespace UniLab.AssetDelivery
{
    /// <summary>
    /// Represents delivery failures that application boot and loading flows should handle as retryable infrastructure errors.
    /// </summary>
    public class AssetDeliveryException : Exception
    {
        /// <summary>
        /// Creates an asset delivery exception with a caller-facing failure message.
        /// </summary>
        public AssetDeliveryException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates an asset delivery exception that preserves the underlying platform or Addressables failure for diagnostics.
        /// </summary>
        public AssetDeliveryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
