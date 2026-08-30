using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.UI.Focus;

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

        /// <summary>
        /// フォーカスグリッドの積み先を差し替える。パッド操作を行う場合のみ必要で、
        /// 未接続なら PopupService はフォーカス制御を一切行わない（ポインタ専用プロジェクトはこのままでよい）。
        /// FocusNavigator はシーンごとに寿命を持つのに対し PopupService は常駐のため、
        /// コンストラクタ注入ではなく各シーンの初期化時にこのメソッドで渡す。
        /// </summary>
        void AttachFocusNavigator(IFocusNavigator focusNavigator);

        /// <summary>シーン破棄時に呼ぶ。破棄済みの FocusNavigator を握り続けないようにする。</summary>
        void DetachFocusNavigator();

        /// <summary>
        /// 表示中の全ポップアップを最前面から順に強制的に閉じ、全て閉じ終わるまで待つ。
        /// シーン遷移・ログアウト時の一括クローズ用。待機列の未表示要求は対象外。
        /// </summary>
        UniTask CloseAllAsync();
    }
}
