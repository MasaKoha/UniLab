using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 複数の Transition を同時再生する合成 Transition。例: 中身をスケールしつつ暗幕をフェードさせる。
    /// PopupBase からはこの 1 つを参照させ、子 Transition は本コンポーネントからのみ駆動する。
    /// </summary>
    public sealed class CompositePopupTransition : PopupTransitionBase
    {
        [SerializeField] private PopupTransitionBase[] _transitions = null;

        /// <summary>全ての子 Transition の開くアニメーションを同時に再生し、全完了まで待つ。</summary>
        public override UniTask PlayOpenAsync(CancellationToken cancellationToken)
        {
            return UniTask.WhenAll(BuildTasks(true, cancellationToken));
        }

        /// <summary>全ての子 Transition の閉じるアニメーションを同時に再生し、全完了まで待つ。</summary>
        public override UniTask PlayCloseAsync(CancellationToken cancellationToken)
        {
            return UniTask.WhenAll(BuildTasks(false, cancellationToken));
        }

        // 開閉は毎フレーム呼ばれないため配列確保のコストは許容する
        private UniTask[] BuildTasks(bool open, CancellationToken cancellationToken)
        {
            var tasks = new UniTask[_transitions.Length];
            for (var index = 0; index < _transitions.Length; index++)
            {
                tasks[index] = open
                    ? _transitions[index].PlayOpenAsync(cancellationToken)
                    : _transitions[index].PlayCloseAsync(cancellationToken);
            }

            return tasks;
        }
    }
}
