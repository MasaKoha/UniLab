using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace UniLab.AssetVault
{
    /// <summary>
    /// 起動処理やロードフローから Addressables の詳細を隠す、アプリケーション向け asset vault API を定義します。
    /// </summary>
    public interface IAssetVaultService
    {
        /// <summary>
        /// アプリケーションのロード UI が表示状態の切り替えに使う、現在の配信状態を取得します。
        /// </summary>
        ReadOnlyReactiveProperty<AssetVaultState> State { get; }

        /// <summary>
        /// DownloadAsync の実行中に依存関係のダウンロード進捗を通知し、progress UI がポーリングなしで更新できるようにします。
        /// </summary>
        Observable<DownloadProgress> OnDownloadProgress { get; }

        /// <summary>
        /// 起動時に配信システムを一度だけ初期化します。env に対応する <paramref name="baseUrl"/>（env→URL のマッピングはアプリ config が持つ）を受け取り、
        /// 初期化前に BaseUrl を確定させます。版（ContentPath）は baseUrl の version.json から解決します。
        /// Debug Override が有効な場合はそちらの BaseUrl を優先します。baseUrl が空なら Local 専用として version 解決をスキップします。
        /// </summary>
        UniTask InitializeAsync(string baseUrl, CancellationToken cancellationToken);

        /// <summary>
        /// 起動時にリモート catalog の更新を確認し、検出した catalog 変更を適用してから結果を返します。
        /// </summary>
        UniTask<CatalogUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// アプリケーションが確認ダイアログの要否を判断できるよう、label の依存関係ダウンロードサイズを取得します。
        /// </summary>
        UniTask<long> GetDownloadSizeAsync(IReadOnlyList<string> labels, CancellationToken cancellationToken);

        /// <summary>
        /// gameplay や画面遷移の前に label の依存関係をダウンロードし、OnDownloadProgress で進捗を通知します。
        /// </summary>
        UniTask DownloadAsync(IReadOnlyList<string> labels, CancellationToken cancellationToken);

        /// <summary>
        /// 呼び出し側が全 asset load に使う画面または scene lifetime の scope を作成し、解放の所有権を集約します。
        /// </summary>
        IAssetScope CreateScope();

        /// <summary>
        /// デバッグツールやストレージ逼迫時の復旧フローから要求されたときに、キャッシュ済み配信データを削除します。
        /// </summary>
        UniTask<bool> ClearCacheAsync(CancellationToken cancellationToken);
    }
}
