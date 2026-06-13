namespace UniLab.AssetDelivery
{
    /// <summary>
    /// Represents the runtime state that application loading UI observes while asset delivery is prepared or downloading.
    /// </summary>
    public enum AssetDeliveryState
    {
        /// <summary>
        /// The service has not been initialized by the boot sequence.
        /// </summary>
        NotInitialized,

        /// <summary>
        /// The boot sequence is initializing the delivery system and loading catalog data.
        /// </summary>
        Initializing,

        /// <summary>
        /// The delivery system is ready for update checks, downloads, and scoped asset loading.
        /// </summary>
        Ready,

        /// <summary>
        /// The service is downloading dependencies requested by the application.
        /// </summary>
        Downloading,

        /// <summary>
        /// The last initialization or download operation failed and the application may retry initialization.
        /// </summary>
        Failed
    }
}
