using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.UI.Tween;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 対象 Transform を 0 倍↔等倍でスケールさせる開閉アニメーション。開くときに少し行き過ぎてから収まる。
    /// </summary>
    public sealed class ScalePopupTransition : PopupTransitionBase
    {
        private const float OpenDuration = 0.25f;
        private const float CloseDuration = 0.2f;

        [SerializeField] private Transform _target = null;

        /// <summary>0 倍から等倍へ、勢いをつけて開く。</summary>
        public override async UniTask PlayOpenAsync(CancellationToken cancellationToken)
        {
            await UiTween.ScaleAsync(
                _target, Vector3.zero, Vector3.one, OpenDuration, EaseType.OutBack, cancellationToken);
        }

        /// <summary>現在の倍率から 0 倍へ縮小して閉じる。</summary>
        public override async UniTask PlayCloseAsync(CancellationToken cancellationToken)
        {
            await UiTween.ScaleAsync(
                _target, _target.localScale, Vector3.zero, CloseDuration, EaseType.InBack, cancellationToken);
        }
    }
}
