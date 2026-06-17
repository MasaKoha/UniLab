using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace UniLab.AssetVault
{
    /// <summary>
    /// アプリ起動時の「初期化 → カタログ更新確認 → 初期必須アセットの事前ダウンロード」を1呼び出しに束ねる起動シーケンス部品です。
    /// 個々の部品（InitializeAsync / AssetVaultUpdateController）の呼び出し順とリトライ・失敗ハンドリングをここに集約し、
    /// アプリ側は BaseUrl・初期DLラベル・確認ダイアログを渡して StartAsync を1回呼ぶだけで起動準備を完了できます。
    /// </summary>
    public sealed class AssetVaultBootstrapper
    {
        private readonly IAssetVaultService _assetVaultService;
        private readonly AssetVaultUpdateController _updateController;

        /// <summary>
        /// 対象の vault service を注入して起動シーケンスを作成します。内部でカタログ更新・DL 用の AssetVaultUpdateController を生成します。
        /// 実プロジェクトでは VContainer 等で IAssetVaultService を注入します。
        /// </summary>
        public AssetVaultBootstrapper(IAssetVaultService assetVaultService)
        {
            _assetVaultService = assetVaultService;
            _updateController = new AssetVaultUpdateController(assetVaultService);
        }

        /// <summary>
        /// 配信基盤“全体”の状態を取得します。アプリは全体ローディング UI（初期化中・DL 中・失敗）の表示制御に使います。
        /// </summary>
        public ReadOnlyReactiveProperty<AssetVaultState> State => _assetVaultService.State;

        /// <summary>
        /// 初期事前ダウンロード実行中の進捗を通知します。委譲先 controller のストリームをそのまま公開します。
        /// </summary>
        public Observable<DownloadProgress> OnProgress => _updateController.OnProgress;

        /// <summary>
        /// 起動シーケンスを実行します。初期化（リトライ付き）に成功したら、カタログ更新確認と初期必須アセットの事前ダウンロードまで行います。
        /// OperationCanceledException は正常系として呼び出し側へ素通しします。
        /// </summary>
        /// <param name="baseUrl">配信先の BaseUrl。Local 専用なら空文字（version 解決をスキップ）。</param>
        /// <param name="initialDownloadLabels">起動時に先取りしたい必須ラベル群。null/空ならダウンロードをスキップ。</param>
        /// <param name="confirmAsync">サイズ（バイト）を受け取りダウンロード続行可否を返す確認処理。null なら確認をスキップ。</param>
        /// <param name="maxRetryCount">初期化・更新・DL 失敗時の最大リトライ回数。0 ならリトライしない。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        public async UniTask<AssetVaultBootstrapResult> StartAsync(
            string baseUrl,
            IReadOnlyList<string> initialDownloadLabels,
            Func<long, UniTask<bool>> confirmAsync,
            int maxRetryCount,
            CancellationToken cancellationToken)
        {
            // --- 1. 初期化（リトライ付き）。失敗したら以降へ進めないため、ここで打ち切って結果を返す。 ---
            var initialized = await TryInitializeAsync(baseUrl, maxRetryCount, cancellationToken);
            if (!initialized)
            {
                return new AssetVaultBootstrapResult(initialized: false, updateResult: default);
            }

            // --- 2. カタログ更新確認 + 初期必須アセットの事前ダウンロード。 ---
            var updateResult = await _updateController.RunUpdateAsync(
                initialDownloadLabels,
                confirmAsync,
                maxRetryCount,
                cancellationToken);

            return new AssetVaultBootstrapResult(initialized: true, updateResult: updateResult);
        }

        /// <summary>
        /// InitializeAsync を最大 maxRetryCount 回まで指数バックオフでリトライします。
        /// 全て失敗した場合はログを残して false を返します（致命扱いにせず、アプリ側のリトライ導線に委ねる）。キャンセルは素通しします。
        /// </summary>
        private async UniTask<bool> TryInitializeAsync(string baseUrl, int maxRetryCount, CancellationToken cancellationToken)
        {
            // リトライ機構は AssetVaultRetry に集約。ここでは成否を bool へ写し、失敗時のログ＋リトライ導線への委譲に専念する。
            try
            {
                await AssetVaultRetry.RunAsync(
                    token => _assetVaultService.InitializeAsync(baseUrl, token),
                    maxRetryCount,
                    cancellationToken);
                return true;
            }
            catch (AssetVaultException exception)
            {
                Debug.LogError($"[AssetVault] initialization failed after {maxRetryCount + 1} attempt(s). {exception}");
                return false;
            }
        }
    }
}
