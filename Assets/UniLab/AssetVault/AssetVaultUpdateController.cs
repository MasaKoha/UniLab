using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace UniLab.AssetVault
{
    /// <summary>
    /// 「リモート catalog の更新確認 → 差分（指定ラベル）の事前ダウンロード」を一括で面倒を見る再利用部品です。
    /// catalog 確認は自身がリトライ付きで実行し、ダウンロードフローは AssetVaultDownloadController に委譲します。
    /// アプリ側は事前 DL したいラベルと確認ダイアログだけ渡せば、更新～事前取得まで済むようにします。
    /// </summary>
    public sealed class AssetVaultUpdateController
    {
        private readonly IAssetVaultService _assetVaultService;
        private readonly AssetVaultDownloadController _downloadController;

        /// <summary>
        /// 対象の vault service を注入して controller を作成します。ダウンロードフロー用の AssetVaultDownloadController を内部生成します。
        /// 実プロジェクトでは VContainer 等で IAssetVaultService を注入します。
        /// </summary>
        public AssetVaultUpdateController(IAssetVaultService assetVaultService)
        {
            _assetVaultService = assetVaultService;
            _downloadController = new AssetVaultDownloadController(assetVaultService);
        }

        /// <summary>
        /// 事前ダウンロード実行中の依存関係ダウンロード進捗を通知します。委譲先 controller のストリームをそのまま公開します。
        /// </summary>
        public Observable<DownloadProgress> OnProgress => _downloadController.OnProgress;

        /// <summary>
        /// catalog 更新を確認・適用してから、指定ラベルの未取得分を事前ダウンロードします。
        /// catalog 確認はリトライ付きで実行し、ラベルが null/空ならダウンロードはスキップします。
        /// OperationCanceledException は正常系として呼び出し側へ素通しします。
        /// </summary>
        /// <param name="labelsToPredownload">事前ダウンロード対象のラベル群。null/空ならダウンロードをスキップ。</param>
        /// <param name="confirmAsync">サイズ（バイト）を受け取り、ダウンロード続行可否を返す確認処理。null なら確認をスキップ。</param>
        /// <param name="maxRetryCount">catalog 確認・DL 失敗時の最大リトライ回数。0 ならリトライしない。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        public async UniTask<AssetVaultUpdateResult> RunUpdateAsync(
            IReadOnlyList<string> labelsToPredownload,
            Func<long, UniTask<bool>> confirmAsync,
            int maxRetryCount,
            CancellationToken cancellationToken)
        {
            // --- 1. catalog 更新確認（リトライ付き）。確認＋適用まで service が内包する。リトライ機構は AssetVaultRetry に集約。 ---
            CatalogUpdateInfo catalogUpdateInfo;
            try
            {
                catalogUpdateInfo = await AssetVaultRetry.RunAsync(
                    token => _assetVaultService.CheckForUpdatesAsync(token),
                    maxRetryCount,
                    cancellationToken);
            }
            catch (AssetVaultException exception)
            {
                // リトライを尽くしても catalog 確認に失敗。差分の入口が開けないため、ダウンロードへは進まず Failed を返す。
                Debug.LogError($"[AssetVault] catalog update check failed after {maxRetryCount + 1} attempt(s). {exception}");
                return new AssetVaultUpdateResult(
                    catalogUpdated: false,
                    updatedCatalogIds: Array.Empty<string>(),
                    downloadResult: AssetVaultDownloadResult.Failed);
            }

            // --- 2. 事前ダウンロード。対象ラベルが無ければ通信も確認も不要。 ---
            var downloadResult = await PredownloadIfNeededAsync(
                labelsToPredownload,
                confirmAsync,
                maxRetryCount,
                cancellationToken);

            // --- 3. catalog 結果と DL 結果を束ねて返す。 ---
            return new AssetVaultUpdateResult(
                catalogUpdated: catalogUpdateInfo.HasUpdate,
                updatedCatalogIds: catalogUpdateInfo.UpdatedCatalogIds,
                downloadResult: downloadResult);
        }

        /// <summary>
        /// 事前ダウンロード対象ラベルがあればダウンロードフローを実行し、無ければ NothingToDownload を返します。
        /// </summary>
        private UniTask<AssetVaultDownloadResult> PredownloadIfNeededAsync(
            IReadOnlyList<string> labelsToPredownload,
            Func<long, UniTask<bool>> confirmAsync,
            int maxRetryCount,
            CancellationToken cancellationToken)
        {
            if (labelsToPredownload == null || labelsToPredownload.Count <= 0)
            {
                return UniTask.FromResult(AssetVaultDownloadResult.NothingToDownload);
            }

            return _downloadController.EnsureDownloadedAsync(
                labelsToPredownload,
                confirmAsync,
                maxRetryCount,
                cancellationToken);
        }
    }
}
