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
    /// アプリケーション向けのアセット配信サービスを Addressables で実装します。
    /// </summary>
    public sealed class AddressablesAssetDeliveryService : IAssetDeliveryService, IDisposable
    {
        private readonly ReactiveProperty<AssetDeliveryState> _state = new(AssetDeliveryState.NotInitialized);
        private readonly Subject<DownloadProgress> _downloadProgress = new();

        /// <summary>
        /// 起動処理とロード UI が状態遷移を監視する現在の配信状態を取得します。
        /// </summary>
        public ReadOnlyReactiveProperty<AssetDeliveryState> State => _state;

        /// <summary>
        /// 配信失敗時もストリームを終了せず、依存関係のダウンロード進捗を通知します。
        /// </summary>
        public Observable<DownloadProgress> OnDownloadProgress => _downloadProgress;

        /// <summary>
        /// Addressables を初期化し、成功時にサービスを準備完了状態へ移行します。
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
                throw AssetDeliveryOperationGuard.ToAssetDeliveryException(exception, "Failed to initialize asset delivery.");
            }
        }

        /// <summary>
        /// リモート catalog を確認して検出した更新を適用し、catalog の変更概要を返します。
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
                throw AssetDeliveryOperationGuard.ToAssetDeliveryException(exception, "Failed to check or apply catalog updates.");
            }
        }

        /// <summary>
        /// 呼び出し側が任意ダウンロード UI の表示を制御できるよう、label の依存関係ダウンロード総量を取得します。
        /// </summary>
        public async UniTask<long> GetDownloadSizeAsync(IReadOnlyList<string> labels, CancellationToken cancellationToken)
        {
            try
            {
                return await Addressables.GetDownloadSizeAsync((IEnumerable)labels).ToUniTask(cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw AssetDeliveryOperationGuard.ToAssetDeliveryException(exception, "Failed to get dependency download size.");
            }
        }

        /// <summary>
        /// label の依存関係をダウンロードし、handle 解放後もキャッシュ済み bundle を保持しながら進捗を通知します。
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
                AssetDeliveryOperationGuard.ThrowIfFailed(handle, "Failed to download asset dependencies.");
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
                throw AssetDeliveryOperationGuard.ToAssetDeliveryException(exception, "Failed to download asset dependencies.");
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
        /// 呼び出し側が Addressables handle の解放を画面 lifetime に紐づけられる scoped asset loader を作成します。
        /// </summary>
        public IAssetScope CreateScope()
        {
            return new AssetScope();
        }

        /// <summary>
        /// デバッグツールやストレージ復旧フローから要求されたときに、キャッシュ済み bundle を削除します。
        /// </summary>
        public async UniTask<bool> ClearCacheAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await Addressables.CleanBundleCache().ToUniTask(cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw AssetDeliveryOperationGuard.ToAssetDeliveryException(exception, "Failed to clear asset delivery cache.");
            }
        }

        /// <summary>
        /// このサービスインスタンスが所有する Observable リソースを解放します。
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
                    // perf: 進捗は毎フレームポーリングされるため、Addressables が変更を報告した場合だけ通知する。
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

    }
}
