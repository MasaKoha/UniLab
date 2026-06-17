using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace UniLab.AssetVault
{
    /// <summary>
    /// Remote アセットの事前ダウンロードを「サイズ確認 → ユーザー確認 → リトライ付き DL → 進捗通知」まで一括で面倒を見る再利用部品です。
    /// IAssetVaultService の薄いラッパーとして振る舞い、アプリ側はラベルと確認ダイアログだけ渡せば済むようにします。
    /// </summary>
    public sealed class AssetVaultDownloadController
    {
        private readonly IAssetVaultService _assetVaultService;

        /// <summary>
        /// ダウンロード対象の vault service を注入して controller を作成します。実プロジェクトでは VContainer 等で注入します。
        /// </summary>
        public AssetVaultDownloadController(IAssetVaultService assetVaultService)
        {
            _assetVaultService = assetVaultService;
        }

        /// <summary>
        /// DownloadAsync 実行中の依存関係ダウンロード進捗を通知します。service のストリームをそのまま委譲します。
        /// </summary>
        public Observable<DownloadProgress> OnProgress => _assetVaultService.OnDownloadProgress;

        /// <summary>
        /// 指定 label の未取得分を、サイズ確認・ユーザー確認・リトライを挟んでダウンロードします。
        /// confirmAsync が null の場合は確認なしで即ダウンロードします。OperationCanceledException は正常系として呼び出し側へ素通しします。
        /// </summary>
        /// <param name="labels">ダウンロード対象のラベル群。</param>
        /// <param name="confirmAsync">サイズ（バイト）を受け取り、ダウンロード続行可否を返す確認処理。null なら確認をスキップ。</param>
        /// <param name="maxRetryCount">DL 失敗時の最大リトライ回数。0 ならリトライしない。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        public async UniTask<AssetVaultDownloadResult> EnsureDownloadedAsync(
            IReadOnlyList<string> labels,
            Func<long, UniTask<bool>> confirmAsync,
            int maxRetryCount,
            CancellationToken cancellationToken)
        {
            // --- 1. サイズ確認。未取得分が無ければ通信も確認も不要。 ---
            var downloadSizeBytes = await _assetVaultService.GetDownloadSizeAsync(labels, cancellationToken);
            if (downloadSizeBytes <= 0)
            {
                return AssetVaultDownloadResult.NothingToDownload;
            }

            // --- 2. ユーザー確認。拒否されたらダウンロードしない。 ---
            if (confirmAsync != null)
            {
                var confirmed = await confirmAsync(downloadSizeBytes);
                if (!confirmed)
                {
                    return AssetVaultDownloadResult.CanceledByUser;
                }
            }

            // --- 3. リトライ付きダウンロード。 ---
            return await DownloadWithRetryAsync(labels, maxRetryCount, cancellationToken);
        }

        /// <summary>
        /// DownloadAsync を最大 maxRetryCount 回まで指数バックオフでリトライします。キャンセルは再 throw します。
        /// </summary>
        private async UniTask<AssetVaultDownloadResult> DownloadWithRetryAsync(
            IReadOnlyList<string> labels,
            int maxRetryCount,
            CancellationToken cancellationToken)
        {
            AssetVaultException lastException = null;

            for (var attempt = 0; attempt <= maxRetryCount; attempt++)
            {
                try
                {
                    await _assetVaultService.DownloadAsync(labels, cancellationToken);
                    return AssetVaultDownloadResult.Completed;
                }
                catch (AssetVaultException exception)
                {
                    // ロード/通信由来の再試行可能エラー。最後の試行なら抜けてログ＋Failed へ。
                    lastException = exception;
                    if (attempt >= maxRetryCount)
                    {
                        break;
                    }

                    await DelayBeforeRetryAsync(attempt, cancellationToken);
                }
            }

            // 例外メッセージ・ログは英語、コメント/summary は日本語の方針に従う。
            Debug.LogError($"[AssetVault] download failed after {maxRetryCount + 1} attempt(s). {lastException}");
            return AssetVaultDownloadResult.Failed;
        }

        /// <summary>
        /// リトライ前に指数バックオフで待機します。間隔計算は <see cref="AssetVaultRetryPolicy"/> に集約しています。
        /// </summary>
        private static UniTask DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
        {
            var delaySeconds = AssetVaultRetryPolicy.GetBackoffDelaySeconds(attempt);
            return UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: cancellationToken);
        }
    }
}
