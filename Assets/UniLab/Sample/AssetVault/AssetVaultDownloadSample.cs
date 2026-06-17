using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace UniLab.AssetVault.Sample
{
    /// <summary>
    /// AssetVaultDownloadController を使い、「進捗購読 → サイズ確認スタブ → リトライ付き事前ダウンロード」を最小構成で示すサンプルです。
    ///
    /// 実プロジェクトでの推奨形:
    ///   - IAssetVaultService と AssetVaultDownloadController は DI（VContainer）で注入する。
    ///   - confirmAsync には実際の確認ダイアログ（「○○MB ダウンロードします」）を渡す。
    /// 本サンプルは自己完結のため Service を直接 new し、confirmAsync も常に true を返すスタブにしています。
    /// </summary>
    public sealed class AssetVaultDownloadSample : MonoBehaviour
    {
        // 配信先の基底 URL。Local 同梱のみで試すなら空でよい（version 解決はスキップされる）。
        [Header("配信先 BaseUrl（Local のみなら空でよい）")]
        [SerializeField] private string _baseUrl = "";

        // 事前ダウンロード対象ラベル。Local 同梱のみなら空でよい。
        [Header("Remote 事前ダウンロード対象ラベル")]
        [SerializeField] private string[] _preloadLabels = Array.Empty<string>();

        // DL 失敗時の最大リトライ回数。
        [Header("最大リトライ回数")]
        [SerializeField] private int _maxRetryCount = 3;

        // 自己完結サンプルのため new。実プロジェクトでは IAssetVaultService をコンストラクタ注入する。
        private readonly AddressablesAssetVaultService _assetVaultService = new();

        // 進捗購読をまとめて破棄するためのコンテナ（OnDestroy で Dispose）。
        private readonly CompositeDisposable _compositeDisposable = new();

        // service を包む再利用部品。実プロジェクトでは VContainer で注入する。
        private AssetVaultDownloadController _downloadController;

        private void Start()
        {
            _downloadController = new AssetVaultDownloadController(_assetVaultService);

            // 進捗（0..1）を購読してパーセント表示。DownloadAsync 実行中のみ発火する。
            _downloadController.OnProgress
                .Subscribe(progress => Debug.Log($"[AssetVault] download {progress.Ratio:P0}"))
                .AddTo(_compositeDisposable);

            // destroyCancellationToken を使うので、画面破棄でフローが自動キャンセルされる。
            RunAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 初期化してから、リトライ付きの事前ダウンロードを実行する一連フロー。
        /// </summary>
        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _assetVaultService.InitializeAsync(_baseUrl, cancellationToken);

                var result = await _downloadController.EnsureDownloadedAsync(
                    _preloadLabels,
                    ConfirmDownloadAsync,
                    _maxRetryCount,
                    cancellationToken);

                Debug.Log($"[AssetVault] ensure download result = {result}");
            }
            catch (OperationCanceledException)
            {
                // 画面破棄（destroyCancellationToken）などによる正常キャンセル。
            }
        }

        /// <summary>
        /// 確認ダイアログ代わりのスタブ。サイズをログに出し、常に続行（true）を返します。
        /// 実プロジェクトでは「○○MB ダウンロードしますか？」の UI を出し、ユーザーの選択を返します。
        /// </summary>
        private UniTask<bool> ConfirmDownloadAsync(long downloadSizeBytes)
        {
            var megaBytes = downloadSizeBytes / (1024f * 1024f);
            Debug.Log($"[AssetVault] confirm download: {megaBytes:F1} MB");
            return UniTask.FromResult(true);
        }

        private void OnDestroy()
        {
            // 自己完結のため new した Service（State/進捗ストリーム）と、購読だけを破棄する。
            _assetVaultService.Dispose();
            _compositeDisposable.Dispose();
        }
    }
}
