using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// ポップアップの開閉アニメーションを差し替え可能にする抽象。PopupBase が表示/クローズ時に呼ぶ。
    /// </summary>
    public interface IPopupTransition
    {
        /// <summary>開くアニメーションを再生する。表示時に呼ばれる。</summary>
        UniTask PlayOpenAsync(CancellationToken cancellationToken);

        /// <summary>閉じるアニメーションを再生する。クローズ時に呼ばれる。</summary>
        UniTask PlayCloseAsync(CancellationToken cancellationToken);
    }
}
