using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace UniLab.AssetDelivery
{
    /// <summary>
    /// Defines the application-facing asset delivery API that hides Addressables details from boot and loading flows.
    /// </summary>
    public interface IAssetDeliveryService
    {
        /// <summary>
        /// Gets the current delivery state that application loading UI observes to switch visible states.
        /// </summary>
        ReadOnlyReactiveProperty<AssetDeliveryState> State { get; }

        /// <summary>
        /// Emits dependency download progress while DownloadAsync is running so progress UI can update without polling.
        /// </summary>
        Observable<DownloadProgress> OnDownloadProgress { get; }

        /// <summary>
        /// Initializes the delivery system once during boot so catalog and runtime delivery services are ready.
        /// </summary>
        UniTask InitializeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Checks for remote catalog updates during boot and returns the result after applying any discovered catalog changes.
        /// </summary>
        UniTask<CatalogUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the dependency download size for labels so the application can decide whether a confirmation dialog is needed.
        /// </summary>
        UniTask<long> GetDownloadSizeAsync(IReadOnlyList<string> labels, CancellationToken cancellationToken);

        /// <summary>
        /// Downloads dependencies for labels before gameplay or screen entry while reporting progress through OnDownloadProgress.
        /// </summary>
        UniTask DownloadAsync(IReadOnlyList<string> labels, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a screen or scene lifetime scope that callers use for all asset loads to centralize release ownership.
        /// </summary>
        IAssetScope CreateScope();

        /// <summary>
        /// Clears cached delivery data when debug tools or storage pressure recovery flows request cache cleanup.
        /// </summary>
        UniTask<bool> ClearCacheAsync(CancellationToken cancellationToken);
    }
}
