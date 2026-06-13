using System;
using R3;
using UnityEngine;

namespace UniLab.AssetVault.Sample
{
    /// <summary>
    /// presenter が監視および更新する sample UI の contract を定義します。
    /// </summary>
    public interface IAssetVaultSampleView : IDisposable
    {
        /// <summary>
        /// ユーザーがサービス初期化を要求したときに view から通知されるイベントを取得します。
        /// </summary>
        Observable<Unit> OnInitializeRequested { get; }

        /// <summary>
        /// ユーザーが catalog 確認と依存関係ダウンロードを要求したときに view から通知されるイベントを取得します。
        /// </summary>
        Observable<Unit> OnCheckAndDownloadRequested { get; }

        /// <summary>
        /// ユーザーが scoped sprite loading を要求したときに view から通知されるイベントを取得します。
        /// </summary>
        Observable<Unit> OnLoadAssetRequested { get; }

        /// <summary>
        /// ユーザーが cache cleanup を要求したときに view から通知されるイベントを取得します。
        /// </summary>
        Observable<Unit> OnClearCacheRequested { get; }

        /// <summary>
        /// presenter が表示する配信状態テキストを更新します。
        /// </summary>
        void SetStateText(string text);

        /// <summary>
        /// presenter が表示するダウンロード進捗を更新します。
        /// </summary>
        void SetProgress(float ratio);

        /// <summary>
        /// presenter が表示するロード済み sprite の preview を更新します。
        /// </summary>
        void SetLoadedSprite(Sprite sprite);

        /// <summary>
        /// presenter が表示する操作メッセージを更新します。
        /// </summary>
        void SetMessage(string text);
    }
}
