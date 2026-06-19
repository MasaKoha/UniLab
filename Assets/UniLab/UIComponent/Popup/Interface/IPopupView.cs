using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// ポップアップ View のライフサイクル契約。マネージャ / PopupService が各段階で順に呼ぶ。
    /// </summary>
    public interface IPopupView
    {
        /// <summary>パラメータを受け取り初期化する。生成直後に呼ばれる。</summary>
        void Initialize(IPopupParameter parameter);

        /// <summary>開くアニメーションを再生する。表示時に呼ばれる。</summary>
        UniTask OpenAsync();

        /// <summary>ユーザー操作の完了を待機する。</summary>
        UniTask WaitAsync();

        /// <summary>バックキー / 背景タップ時の閉じ処理。</summary>
        void OnClose();

        /// <summary>閉じるアニメーションを再生する。クローズ時に呼ばれる。</summary>
        UniTask CloseAsync();
    }
}
