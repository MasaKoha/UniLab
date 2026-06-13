using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace UniLab.AssetDelivery.Sample
{
    /// <summary>
    /// Coordinates the asset delivery sample view with the delivery service.
    /// </summary>
    public sealed class AssetDeliverySamplePresenter : IDisposable
    {
        private readonly IAssetDeliveryService _service;
        private readonly IAssetDeliverySampleView _view;
        private readonly string _downloadLabel;
        private readonly string _assetKey;
        private readonly CompositeDisposable _compositeDisposable = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly IAssetScope _scope;

        /// <summary>
        /// Creates the presenter and wires sample UI events to the delivery service.
        /// </summary>
        public AssetDeliverySamplePresenter(
            IAssetDeliveryService service,
            IAssetDeliverySampleView view,
            string downloadLabel,
            string assetKey)
        {
            _service = service;
            _view = view;
            _downloadLabel = downloadLabel;
            _assetKey = assetKey;
            _scope = _service.CreateScope();

            // --- Setup subscriptions ---
            _compositeDisposable.Add(_service.State.Subscribe(state => _view.SetStateText(state.ToString())));
            _compositeDisposable.Add(_service.OnDownloadProgress.Subscribe(progress => _view.SetProgress(progress.Ratio)));
            _compositeDisposable.Add(_view.OnInitializeRequested.Subscribe(_ => InitializeAsync(_cancellationTokenSource.Token).Forget()));
            _compositeDisposable.Add(_view.OnCheckAndDownloadRequested.Subscribe(_ => CheckAndDownloadAsync(_cancellationTokenSource.Token).Forget()));
            _compositeDisposable.Add(_view.OnLoadAssetRequested.Subscribe(_ => LoadAssetAsync(_cancellationTokenSource.Token).Forget()));
            _compositeDisposable.Add(_view.OnClearCacheRequested.Subscribe(_ => ClearCacheAsync(_cancellationTokenSource.Token).Forget()));
        }

        /// <summary>
        /// Cancels in-flight operations and releases scoped assets and subscriptions.
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _scope.Dispose();
            _compositeDisposable.Dispose();
            _view.Dispose();
        }

        private async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                _view.SetMessage("Initializing asset delivery.");
                await _service.InitializeAsync(cancellationToken);
                _view.SetMessage("Asset delivery initialized.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (AssetDeliveryException exception)
            {
                _view.SetMessage(exception.Message);
            }
        }

        private async UniTask CheckAndDownloadAsync(CancellationToken cancellationToken)
        {
            try
            {
                _view.SetMessage("Checking catalog updates.");
                var updateInfo = await _service.CheckForUpdatesAsync(cancellationToken);
                if (!updateInfo.HasUpdate)
                {
                    _view.SetMessage("No catalog updates were found.");
                    return;
                }

                var labels = new[] { _downloadLabel };
                var downloadSize = await _service.GetDownloadSizeAsync(labels, cancellationToken);
                _view.SetMessage($"Download size: {downloadSize} bytes.");
                if (downloadSize <= 0L)
                {
                    _view.SetMessage("No dependency download is required.");
                    return;
                }

                await _service.DownloadAsync(labels, cancellationToken);
                _view.SetMessage("Dependency download completed.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (AssetDeliveryException exception)
            {
                _view.SetMessage(exception.Message);
            }
        }

        private async UniTask LoadAssetAsync(CancellationToken cancellationToken)
        {
            try
            {
                _view.SetMessage("Loading sprite asset.");
                var sprite = await _scope.LoadAssetAsync<Sprite>(_assetKey, cancellationToken);
                _view.SetLoadedSprite(sprite);
                _view.SetMessage("Sprite asset loaded.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (AssetDeliveryException exception)
            {
                _view.SetMessage(exception.Message);
            }
        }

        private async UniTask ClearCacheAsync(CancellationToken cancellationToken)
        {
            try
            {
                _view.SetMessage("Clearing asset delivery cache.");
                var cleared = await _service.ClearCacheAsync(cancellationToken);
                _view.SetProgress(0f);
                _view.SetMessage(cleared ? "Asset delivery cache cleared." : "No asset delivery cache was cleared.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (AssetDeliveryException exception)
            {
                _view.SetMessage(exception.Message);
            }
        }
    }
}
