using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AssetVault.Sample
{
    /// <summary>
    /// AssetVault の「初期化 → （任意で更新確認・事前DL）→ ロード → 利用 → 解放」を 1 クラスで示すサンプルです。
    ///
    /// 実プロジェクトでの推奨形:
    ///   - Service（IAssetVaultService）は DI（VContainer）で Singleton 登録し、画面へは注入する。
    ///   - Scope（IAssetScope）は画面の LifetimeScope に Scoped 登録し、画面破棄＝解放を保証する。
    /// 本サンプルは自己完結のため Service を直接 new し、解放を MonoBehaviour の寿命（OnDestroy）に合わせています。
    /// </summary>
    public sealed class AssetVaultSample : MonoBehaviour
    {
        // ロード対象のアドレス。Sync AssetResource 後のアドレス = ルートフォルダ相対・拡張子なし（例 External/Icons/coin.png → "Icons/coin"）。
        [Header("ロードするアドレス（フォルダ相対・拡張子なし）")]
        [SerializeField] private string _spriteAddress = "Icons/coin";
        [SerializeField] private string _prefabAddress = "Enemies/slime";

        // ロード結果の表示先。Inspector で割り当てる。
        [Header("表示先")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Transform _spawnParent;

        // 配信先の基底 URL。InitializeAsync に渡して初期化前に確定させる。
        // 実プロジェクトでは env→URL はアプリ config が持つ。Local 同梱のみで試すなら空でよい（version 解決はスキップされる）。
        [Header("配信先 BaseUrl（Local のみなら空でよい）")]
        [SerializeField] private string _baseUrl = "";

        // Remote(CDN) アセットの事前ダウンロード対象ラベル。Local 同梱アセットのみを使うなら空でよい。
        [Header("Remote 事前ダウンロード（任意。ローカルのみなら空でよい）")]
        [SerializeField] private string[] _preloadLabels = Array.Empty<string>();

        // 自己完結サンプルのため new。実プロジェクトでは IAssetVaultService をコンストラクタ注入する。
        private readonly AddressablesAssetVaultService _assetVaultService = new();

        // R3 の購読をまとめて破棄するためのコンテナ（OnDestroy で Dispose）。
        private readonly CompositeDisposable _compositeDisposable = new();

        // 進行中の非同期ロードを画面破棄時にキャンセルするためのトークン源。
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        // この画面が読み込んだ全アセットの解放を所有するスコープ。Dispose で一括解放。
        private IAssetScope _assetScope;

        private void Start()
        {
            // State は「配信基盤“全体”の状態」を表す（NotInitialized / Initializing / Ready / Downloading / Failed）。
            // ＝ 起動時の初期化中・一括ダウンロード中・失敗 を示すための“全体ローディング UI”用。
            // 注意: 個々の LoadAssetAsync / InstantiateAsync 1 件ごとの進行は State には出ない（State は Ready のまま）。
            //       アセット単体のロード待ちは下の await で扱い、必要ならスピナーを呼び出し側で個別に出す。
            _assetVaultService.State
                .Subscribe(state => Debug.Log($"[AssetVault] state = {state}"))
                .AddTo(_compositeDisposable);

            // 非同期フローを起動。await しないため Forget()（例外は RunAsync 内で処理する）。
            RunAsync(_cancellationTokenSource.Token).Forget();
        }

        /// <summary>
        /// 初期化からロード・表示までの一連フロー。失敗・キャンセルは握って UI へ反映する想定。
        /// </summary>
        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                // --- 1. 初期化（アプリ起動時に 1 回だけ） ---
                // BaseUrl を渡して確定 → version.json で版を解決 → Addressables 初期化＋カタログロード。完了で State=Ready。
                // Debug Override が有効ならそちらの BaseUrl が優先される。
                await _assetVaultService.InitializeAsync(_baseUrl, cancellationToken);

                // --- 2. 更新確認＋事前ダウンロード（Remote を使う場合のみ。Local 同梱だけなら不要） ---
                await PreloadRemoteIfNeededAsync(cancellationToken);

                // --- 3. 画面寿命のスコープを作る ---
                // このスコープ経由で読んだ asset は、scope.Dispose() でまとめて解放される（個別解放は不要）。
                _assetScope = _assetVaultService.CreateScope();

                // --- 4-a. Sprite をロードして Image に適用 ---
                // await の完了＝この 1 枚のロード完了。State は使わず await で待つのがアセット単体の流儀。
                if (_iconImage != null && !string.IsNullOrEmpty(_spriteAddress))
                {
                    _iconImage.sprite = await _assetScope.LoadAssetAsync<Sprite>(_spriteAddress, cancellationToken);
                }

                // --- 4-b. GameObject を生成 ---
                // InstantiateAsync は「生成（Instantiate）」まで行い、生成したインスタンスの破棄もスコープが所有する。
                // ＝ scope.Dispose() でインスタンスごと破棄されるため、ここで作った GameObject を手動 Destroy しない。
                // （プレハブ“資産”だけ欲しく自分で複数 Instantiate したい場合は LoadAssetAsync<GameObject> を使い、
                //   生成インスタンスは自分で Destroy する。詳細は docs/asset-vault-usage.md 参照。）
                if (!string.IsNullOrEmpty(_prefabAddress))
                {
                    await _assetScope.InstantiateAsync(_prefabAddress, _spawnParent, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 画面破棄（OnDestroy の Cancel）などによる正常キャンセル。ログも不要。
            }
            catch (AssetVaultException exception)
            {
                // ロード失敗（キー不一致・カタログ未更新・ネットワーク断 等。InnerException に Addressables の元例外）。
                // 実機ではフォールバック表示やリトライ導線へ繋ぐ。
                Debug.LogError(exception);
            }
        }

        /// <summary>
        /// 指定 label に未取得分があれば事前ダウンロードします。Remote を使わない（ラベル未指定）場合は何もしません。
        /// 「サイズ取得 → 確認 → ダウンロード」の判断材料は基盤が返し、確認 UI を出すのはアプリ側の責務です。
        /// </summary>
        private async UniTask PreloadRemoteIfNeededAsync(CancellationToken cancellationToken)
        {
            // ラベル未指定なら Remote 事前 DL は不要（Local 同梱や都度ロードで足りるケース）。
            if (_preloadLabels == null || _preloadLabels.Length <= 0)
            {
                return;
            }

            // リモートカタログの更新を確認し、あれば適用（差分の入口）。
            var update = await _assetVaultService.CheckForUpdatesAsync(cancellationToken);
            if (update.HasUpdate)
            {
                Debug.Log("[AssetVault] catalog updated.");
            }

            // 未キャッシュ（＝実際に通信が要る）分のサイズ。0 ならダウンロード不要。
            var sizeBytes = await _assetVaultService.GetDownloadSizeAsync(_preloadLabels, cancellationToken);
            if (sizeBytes <= 0)
            {
                return;
            }

            // 進捗（0..1）の購読。DownloadAsync 実行中のみ発火するので、ダウンロード直前に購読する。
            // 実機ではここで「○○MB ダウンロードします」の確認 UI を出してから DownloadAsync する。
            _assetVaultService.OnDownloadProgress
                .Subscribe(progress => Debug.Log($"[AssetVault] download {progress.Ratio:P0}"))
                .AddTo(_compositeDisposable);

            // 依存をまとめて取得。実行中は State=Downloading になり、全体ローディング UI を出せる。
            await _assetVaultService.DownloadAsync(_preloadLabels, cancellationToken);
        }

        private void OnDestroy()
        {
            // 解放順序が重要:
            //   1. 進行中ロードをキャンセル（await を OperationCanceledException で抜けさせる）
            //   2. スコープ破棄でこの画面が読んだ全 asset / 生成 GameObject を一括解放
            //   3. Service を破棄（State/進捗ストリーム等の内部リソース解放）
            //   4. 購読をまとめて破棄
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _assetScope?.Dispose();
            _assetVaultService.Dispose();
            _compositeDisposable.Dispose();
        }
    }
}
