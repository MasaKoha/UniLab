using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// IPopupTransition の MonoBehaviour 基底。ポップアッププレハブにアタッチして PopupBase から参照させる。
    /// インターフェースは SerializeField できないため、差し替え用の共通基底として用意する。
    /// </summary>
    public abstract class PopupTransitionBase : MonoBehaviour, IPopupTransition
    {
        /// <summary>開くアニメーションを再生する。</summary>
        public abstract UniTask PlayOpenAsync(CancellationToken cancellationToken);

        /// <summary>閉じるアニメーションを再生する。</summary>
        public abstract UniTask PlayCloseAsync(CancellationToken cancellationToken);
    }
}
