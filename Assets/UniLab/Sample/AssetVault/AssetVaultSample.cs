using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AssetVault.Sample
{
    /// <summary>
    /// AssetVault の「初期化 → （任意で更新確認・事前DL）→ ロード → 利用」を 1 クラスで示すサンプルです。
    /// ロードは this.LoadAssetAsync 拡張を使い、Scope も Dispose も書きません（この GameObject の破棄で asset は自動 Release）。
    ///
    /// 実プロジェクトでの推奨形:
    ///   - Service（IAssetVaultService）は DI（VContainer）で Singleton 登録し、起動シーケンスで InitializeAsync する。
    ///   - 個々の画面/コンポーネントのロードは this.LoadAssetAsync（破棄連動）で済ませる。
    /// 本サンプルは自己完結のため Service を直接 new し、初期化フローも内包しています。
    /// </summary>
    public sealed class AssetVaultSample : MonoBehaviour
    {
        // 配信先の基底 URL。InitializeAsync に渡して初期化前に確定させる。env→URL はアプリ config の責務。
        // Local 同梱のみで試すなら空でよい（version 解決はスキップされる）。
        [Header("配信先 BaseUrl（Local のみなら空でよい）")]
        [SerializeField] private string _baseUrl = "";

        // ロード対象のアドレス（Sync 後のアドレス = ルートフォルダ相対・拡張子なし。例 "Icons/coin"）。
        [Header("ロードするアドレス（フォルダ相対・拡張子なし）")]
        [SerializeField] private string _spriteAddress = "Icons/coin";
        [SerializeField] private string _prefabAddress = "Enemies/slime";

        // ロード結果の表示先。
        [Header("表示先")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Transform _spawnParent;

        // Remote(CDN) アセットの事前ダウンロード対象ラベル。Local 同梱のみなら空でよい。
        [Header("Remote 事前ダウンロード（任意。ローカルのみなら空でよい）")]
        [SerializeField] private string[] _preloadLabels = Array.Empty<string>();

        // 自己完結サンプルのため new。実プロジェクトでは IAssetVaultService をコンストラクタ注入する。
        private readonly AddressablesAssetVaultService _assetVaultService = new();

        // R3 の購読をまとめて破棄するためのコンテナ（OnDestroy で Dispose）。
        private readonly CompositeDisposable _compositeDisposable = new();

        private void Start()
        {
            // State は「配信基盤“全体”の状態」（NotInitialized / Initializing / Ready / Downloading / Failed）。
            // ＝ 起動時の初期化中・一括ダウンロード中・失敗 を示す“全体ローディング UI”用。
            // 注意: 個々のロード 1 件ごとの進行は State に出ない（State は Ready のまま）。アセット単体は await で待つ。
            _assetVaultService.State
                .Subscribe(state => Debug.Log($"[AssetVault] state = {state}"))
                .AddTo(_compositeDisposable);

            // destroyCancellationToken を使うので、画面破棄でフロー（と各ロード）が自動キャンセルされる。
            RunAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 初期化からロード・表示までの一連フロー。失敗・キャンセルは握って UI へ反映する想定。
        /// </summary>
        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                // --- 1. 初期化（アプリ起動時に 1 回だけ） ---
                // BaseUrl を渡して確定 → version.json で版を解決 → Addressables 初期化＋カタログロード。
                // Debug Override が有効ならそちらの BaseUrl が優先される。
                await _assetVaultService.InitializeAsync(_baseUrl, cancellationToken);

                // --- 2. 更新確認＋事前ダウンロード（Remote を使う場合のみ） ---
                await PreloadRemoteIfNeededAsync(cancellationToken);

                // --- 3. ロードして利用（Scope を書かない） ---
                // this.LoadAssetAsync は この GameObject に紐づくスコープを裏で使い、破棄時に自動 Release する。
                // ＝ CreateScope / Dispose を書かなくてよい。
                if (_iconImage != null && !string.IsNullOrEmpty(_spriteAddress))
                {
                    _iconImage.sprite = await this.LoadAssetAsync<Sprite>(_spriteAddress, cancellationToken);
                }

                // 生成も同様。生成物の破棄もこの GameObject の破棄に連動する（手動 Destroy しない）。
                if (!string.IsNullOrEmpty(_prefabAddress))
                {
                    await this.InstantiateAsync(_prefabAddress, _spawnParent, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 画面破棄（destroyCancellationToken）などによる正常キャンセル。
            }
            catch (AssetVaultException exception)
            {
                // ロード失敗（キー不一致・カタログ未更新・ネットワーク断 等。InnerException に Addressables の元例外）。
                Debug.LogError(exception);
            }
        }

        /// <summary>
        /// 指定 label に未取得分があれば事前ダウンロードします。ラベル未指定なら何もしません。
        /// 「サイズ取得 → 確認 → ダウンロード」の判断材料は基盤が返し、確認 UI を出すのはアプリ側の責務です。
        /// </summary>
        private async UniTask PreloadRemoteIfNeededAsync(CancellationToken cancellationToken)
        {
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
            // ロードした asset / 生成物は this.LoadAssetAsync 拡張により GameObject 破棄で自動解放されるため、ここでは扱わない。
            // 自己完結のため new した Service（State/進捗ストリーム）と、購読だけを破棄する。
            _assetVaultService.Dispose();
            _compositeDisposable.Dispose();
        }
    }
}
