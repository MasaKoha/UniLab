using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniLab.AssetVault
{
    /// <summary>
    /// アプリケーション向けのアセット配信サービスを Addressables で実装します。
    /// </summary>
    public sealed class AddressablesAssetVaultService : IAssetVaultService, IDisposable
    {
        private readonly ReactiveProperty<AssetVaultState> _state = new(AssetVaultState.NotInitialized);
        private readonly Subject<DownloadProgress> _downloadProgress = new();
        private readonly Func<string, IContentVersionResolver> _contentVersionResolverFactory;

        /// <summary>
        /// 既定では version.json を HTTP 取得する <see cref="RemoteContentVersionResolver"/> を使います。
        /// BFF 解決やテストでは <paramref name="contentVersionResolverFactory"/> を差し替えます。
        /// </summary>
        public AddressablesAssetVaultService(Func<string, IContentVersionResolver> contentVersionResolverFactory = null)
        {
            _contentVersionResolverFactory = contentVersionResolverFactory ?? (baseUrl => new RemoteContentVersionResolver(baseUrl));
        }

        /// <summary>
        /// 起動処理とロード UI が状態遷移を監視する現在の配信状態を取得します。
        /// </summary>
        public ReadOnlyReactiveProperty<AssetVaultState> State => _state;

        /// <summary>
        /// 配信失敗時もストリームを終了せず、依存関係のダウンロード進捗を通知します。
        /// </summary>
        public Observable<DownloadProgress> OnDownloadProgress => _downloadProgress;

        /// <summary>
        /// BaseUrl を確定し、版を version.json から解決してから Addressables を初期化します。成功時に準備完了へ移行します。
        /// </summary>
        public async UniTask InitializeAsync(string baseUrl, CancellationToken cancellationToken)
        {
            _state.Value = AssetVaultState.Initializing;

            try
            {
                // Debug Override が BeforeSceneLoad で BaseUrl を入れていればそれを優先（QA）。無ければ引数を採用する。
                // RemoteLoadPath トークンは AssetVaultRuntime を型名参照するため、初期化“前”に確定させる必要がある。
                var effectiveBaseUrl = string.IsNullOrEmpty(AssetVaultRuntime.BaseUrl) ? baseUrl : AssetVaultRuntime.BaseUrl;
                AssetVaultRuntime.BaseUrl = effectiveBaseUrl;

                // 版（ContentPath）は version.json 解決に任せる。Local 専用（baseUrl 空）の場合は解決をスキップする。
                if (!string.IsNullOrEmpty(effectiveBaseUrl))
                {
                    var contentVersion = await _contentVersionResolverFactory(effectiveBaseUrl).ResolveAsync(cancellationToken);
                    AssetVaultRuntime.ContentPath = contentVersion.Path;
                }

                await Addressables.InitializeAsync().ToUniTask(cancellationToken: cancellationToken);
                _state.Value = AssetVaultState.Ready;
            }
            catch (OperationCanceledException)
            {
                // 初期化キャンセルは正常系。まだ準備できていないため NotInitialized へ戻し、アプリ側の再初期化に委ねる。
                _state.Value = AssetVaultState.NotInitialized;
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _state.Value = AssetVaultState.Failed;
                throw AssetVaultOperationGuard.ToAssetVaultException(exception, "Failed to initialize asset vault.");
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
                throw AssetVaultOperationGuard.ToAssetVaultException(exception, "Failed to check or apply catalog updates.");
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
                throw AssetVaultOperationGuard.ToAssetVaultException(exception, "Failed to get dependency download size.");
            }
        }

        /// <summary>
        /// label の依存関係をダウンロードし、handle 解放後もキャッシュ済み bundle を保持しながら進捗を通知します。
        /// </summary>
        public async UniTask DownloadAsync(IReadOnlyList<string> labels, CancellationToken cancellationToken)
        {
            _state.Value = AssetVaultState.Downloading;
            var handle = default(AsyncOperationHandle);
            var hasHandle = false;

            try
            {
                handle = Addressables.DownloadDependenciesAsync(labels, Addressables.MergeMode.Union, autoReleaseHandle: false);
                hasHandle = true;

                await PollDownloadProgressAsync(handle, cancellationToken);
                AssetVaultOperationGuard.ThrowIfFailed(handle, "Failed to download asset dependencies.");
                _state.Value = AssetVaultState.Ready;
            }
            catch (OperationCanceledException)
            {
                // DL キャンセルは正常系。初期化は済んでおり配信基盤は使えるため、Failed ではなく Ready へ戻す。
                _state.Value = AssetVaultState.Ready;
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _state.Value = AssetVaultState.Failed;
                throw AssetVaultOperationGuard.ToAssetVaultException(exception, "Failed to download asset dependencies.");
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
                throw AssetVaultOperationGuard.ToAssetVaultException(exception, "Failed to clear asset vault cache.");
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
