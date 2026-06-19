using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// ポップアップ表示の中心 API。View ロード→優先度キュー→表示→結果待ち→クローズ→解放までを一括で担う。
    /// </summary>
    public interface IPopupService
    {
        /// <summary>表示中のポップアップがあるか。入力ブロックやバックキー処理の判定に購読する。</summary>
        ReadOnlyReactiveProperty<bool> HasActivePopup { get; }

        /// <summary>
        /// ポップアップを表示し結果を待つ。要求は優先度順に直列化され、キャンセル・例外時も View を必ず解放する。
        /// </summary>
        UniTask<TResult> ShowAsync<TPopup, TResult>(
            IPopupParameter parameter, CancellationToken cancellationToken = default)
            where TPopup : PopupBase<TResult>;

        /// <summary>表示中のポップアップをバックキー相当で閉じる。表示中でなければ何もしない。</summary>
        UniTask CloseTopAsync();
    }
}
