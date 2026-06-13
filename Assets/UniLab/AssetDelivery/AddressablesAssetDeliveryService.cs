using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniLab.AssetDelivery
{
    /// <summary>
    /// Provides the Addressables-backed implementation of the application-facing asset delivery service.
    /// </summary>
    public sealed class AddressablesAssetDeliveryService : IAssetDeliveryService, IDisposable
    {
        private readonly ReactiveProperty<AssetDeliveryState> _state = new(AssetDeliveryState.NotInitialized);
        private readonly Subject<DownloadProgress> _downloadProgress = new();

        /// <summary>
        /// Gets the current delivery state that boot and loading UI observe for state transitions.
        /// </summary>
        public ReadOnlyReactiveProperty<AssetDeliveryState> State => _state;

        /// <summary>
        /// Emits dependency download progress without terminating the stream on delivery failures.
        /// </summary>
        public Observable<DownloadProgress> OnDownloadProgress => _downloadProgress;

        /// <summary>
        /// Initializes Addressables and moves the service into the ready state when initialization succeeds.
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            _state.Value = AssetDeliveryState.Initializing;

            try
            {
                await Addressables.InitializeAsync().ToUniTask(cancellationToken: cancellationToken);
                _state.Value = AssetDeliveryState.Ready;
            }
            catch (OperationCanceledException)
            {
                _state.Value = AssetDeliveryState.NotInitialized;
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _state.Value = AssetDeliveryState.Failed;
                throw ToAssetDeliveryException(exception, "Failed to initialize asset delivery.");
            }
        }

        /// <summary>
        /// Checks remote catalogs and applies discovered updates before returning the catalog change summary.
        /// </summary>
        public async UniTask<CatalogUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var catalogIds = await Addressables.CheckForCatalogUpdates().ToUniTask(cancellationToken: cancellationToken);
                if (catalogIds.Count == 0)
                {
                    return new CatalogUpdateInfo(false, Array.Empty<string>());
                }

                await Addressables.UpdateCatalogs(catalogIds).ToUniTask(cancellationToken: cancellationToken);
                return new CatalogUpdateInfo(true, catalogIds);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw ToAssetDeliveryException(exception, "Failed to check or apply catalog updates.");
            }
        }

        /// <summary>
        /// Gets the total dependency download size for labels so callers can gate optional download UI.
        /// </summary>
        public async UniTask<long> GetDownloadSizeAsync(IReadOnlyList<string> labels, CancellationToken cancellationToken)
        {
            try
            {
                return await Addressables.GetDownloadSizeAsync((IEnumerable)labels).ToUniTask(cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw ToAssetDeliveryException(exception, "Failed to get dependency download size.");
            }
        }

        /// <summary>
        /// Downloads label dependencies and reports progress while preserving cached bundles after handle release.
        /// </summary>
        public async UniTask DownloadAsync(IReadOnlyList<string> labels, CancellationToken cancellationToken)
        {
            _state.Value = AssetDeliveryState.Downloading;
            var handle = default(AsyncOperationHandle);
            var hasHandle = false;

            try
            {
                handle = Addressables.DownloadDependenciesAsync(labels, Addressables.MergeMode.Union, autoReleaseHandle: false);
                hasHandle = true;

                await PollDownloadProgressAsync(handle, cancellationToken);
                ThrowIfFailed(handle, "Failed to download asset dependencies.");
                _state.Value = AssetDeliveryState.Ready;
            }
            catch (OperationCanceledException)
            {
                _state.Value = AssetDeliveryState.Ready;
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _state.Value = AssetDeliveryState.Failed;
                throw ToAssetDeliveryException(exception, "Failed to download asset dependencies.");
            }
            finally
            {
                if (hasHandle)
                {
                    Addressables.Release(handle);
                }
            }
        }

        /// <summary>
        /// Creates a scoped asset loader so callers can bind Addressables handle release to screen lifetime.
        /// </summary>
        public IAssetScope CreateScope()
        {
            return new AssetScope();
        }

        /// <summary>
        /// Cleans cached bundles when debug tools or storage recovery flows request cache cleanup.
        /// </summary>
        public async UniTask<bool> ClearCacheAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await Addressables.CleanBundleCache().ToUniTask(cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw ToAssetDeliveryException(exception, "Failed to clear asset delivery cache.");
            }
        }

        /// <summary>
        /// Releases observable resources owned by this service instance.
        /// </summary>
        public void Dispose()
        {
            _state.Dispose();
            _downloadProgress.Dispose();
        }

        private async UniTask PollDownloadProgressAsync(AsyncOperationHandle handle, CancellationToken cancellationToken)
        {
            var lastDownloadedBytes = -1L;
            var lastTotalBytes = -1L;
            var lastRatio = -1f;

            while (!handle.IsDone)
            {
                var status = handle.GetDownloadStatus();
                if (HasProgressChanged(status, lastDownloadedBytes, lastTotalBytes, lastRatio))
                {
                    // perf: progress is polled every frame, so emit only when Addressables reports changed values.
                    _downloadProgress.OnNext(new DownloadProgress(status.DownloadedBytes, status.TotalBytes, status.Percent));
                    lastDownloadedBytes = status.DownloadedBytes;
                    lastTotalBytes = status.TotalBytes;
                    lastRatio = status.Percent;
                }

                await UniTask.Yield(cancellationToken);
            }

            var finalStatus = handle.GetDownloadStatus();
            _downloadProgress.OnNext(new DownloadProgress(finalStatus.DownloadedBytes, finalStatus.TotalBytes, finalStatus.Percent));
        }

        private static bool HasProgressChanged(
            DownloadStatus status,
            long lastDownloadedBytes,
            long lastTotalBytes,
            float lastRatio)
        {
            if (status.DownloadedBytes != lastDownloadedBytes)
            {
                return true;
            }

            if (status.TotalBytes != lastTotalBytes)
            {
                return true;
            }

            return Math.Abs(status.Percent - lastRatio) > float.Epsilon;
        }

        private static void ThrowIfFailed(AsyncOperationHandle handle, string message)
        {
            if (handle.Status != AsyncOperationStatus.Failed)
            {
                return;
            }

            var exception = handle.OperationException ?? new InvalidOperationException(message);
            throw new AssetDeliveryException(message, exception);
        }

        private static AssetDeliveryException ToAssetDeliveryException(Exception exception, string message)
        {
            if (exception is AssetDeliveryException assetDeliveryException)
            {
                return assetDeliveryException;
            }

            return new AssetDeliveryException(message, exception);
        }
    }
}
