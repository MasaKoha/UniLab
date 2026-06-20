using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.UI.Tween;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 対象 CanvasGroup の alpha を 0↔1 でフェードさせる開閉アニメーション。
    /// </summary>
    public sealed class FadePopupTransition : PopupTransitionBase
    {
        private const float OpenDuration = 0.25f;
        private const float CloseDuration = 0.2f;

        [SerializeField] private CanvasGroup _target = null;

        /// <summary>透明から不透明へフェードインして開く。</summary>
        public override async UniTask PlayOpenAsync(CancellationToken cancellationToken)
        {
            await UiTween.FadeAsync(
                _target, 0f, 1f, OpenDuration, EaseType.OutQuad, cancellationToken);
        }

        /// <summary>現在の不透明度から透明へフェードアウトして閉じる。</summary>
        public override async UniTask PlayCloseAsync(CancellationToken cancellationToken)
        {
            await UiTween.FadeAsync(
                _target, _target.alpha, 0f, CloseDuration, EaseType.InQuad, cancellationToken);
        }
    }
}
