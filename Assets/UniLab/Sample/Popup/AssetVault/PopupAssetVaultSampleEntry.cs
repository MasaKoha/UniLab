using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.AssetVault;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup.AssetVaultSample
{
    /// <summary>
    /// AssetVault(Addressables) 経由で Popup をロードする検証用サンプル。
    /// ローカル初期化（baseUrl 空）で AddressablesAssetVaultService を起動し、AssetVaultPopupAssetLoader で表示する。
    /// 事前に ConfirmPopup プレハブを Addressable 化し、アドレス "Popup/ConfirmPopup" を付与しておくこと。
    /// </summary>
    public sealed class PopupAssetVaultSampleEntry : MonoBehaviour
    {
        [SerializeField] private Transform _popupRoot = null;
        [SerializeField] private PopupDimmer _dimmer = null;
        [SerializeField] private Button _showButton = null;

        // 自己完結サンプルのため new。実プロジェクトでは IAssetVaultService をコンストラクタ注入する
        private readonly AddressablesAssetVaultService _assetVaultService = new();
        private IPopupService _popupService = null;
        private readonly CompositeDisposable _disposables = new();

        private void Start()
        {
            RunAsync(destroyCancellationToken).Forget();
        }

        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Local 専用: baseUrl 空で version 解決・ダウンロードをスキップする
                await _assetVaultService.InitializeAsync(string.Empty, cancellationToken);

                // ロード手段を AssetVault 版に差し替えるだけ。表示・スタック・暗幕の挙動はコアと共通
                var viewProvider = new PopupViewProvider(
                    new AssetVaultPopupAssetLoader(_assetVaultService), _popupRoot);
                _popupService = new PopupService(viewProvider, _dimmer);

                _showButton.OnClickAsObservable()
                    .Subscribe(_ => ShowConfirmAsync(destroyCancellationToken).Forget())
                    .AddTo(_disposables);
            }
            catch (OperationCanceledException)
            {
                // 画面破棄による正常キャンセル
            }
            catch (AssetVaultException exception)
            {
                Debug.LogError(exception);
            }
        }

        private async UniTask ShowConfirmAsync(CancellationToken cancellationToken)
        {
            // アドレス "Popup/ConfirmPopup" を AssetVault がロード → AssetScope に紐づけ → 閉じたら Dispose で解放
            var result = await _popupService.ShowAsync<ConfirmPopup, PopupResult>(
                new PopupParameter
                {
                    Title = "AssetVault",
                    Message = "Loaded via Addressables.",
                    ConfirmLabel = "OK",
                    CancelLabel = "Cancel",
                },
                cancellationToken);

            Debug.Log($"[Popup/AssetVault] result: {result}");
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
            // 初期化完了前に破棄され得るため null を確認する
            if (_popupService != null)
            {
                ((IDisposable)_popupService).Dispose();
            }

            _assetVaultService.Dispose();
        }
    }
}
