using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Tween
{
    /// <summary>
    /// UniTask ベースの軽量 UI 補間。DOTween を持ち込めない共通ライブラリ内で、
    /// スケール / フェードのアニメーションを提供する。Time.timeScale=0 でも動くよう unscaled 時間で進める。
    /// </summary>
    public static class UiTween
    {
        /// <summary>
        /// Transform の localScale を from から to へ補間する。Back 系で 1 を超えるため LerpUnclamped を使う。
        /// cancellationToken でアニメーション中断（シーン遷移・破棄）に対応する。
        /// </summary>
        public static async UniTask ScaleAsync(
            Transform target,
            Vector3 from,
            Vector3 to,
            float duration,
            EaseType easeType,
            CancellationToken cancellationToken)
        {
            if (duration <= 0f)
            {
                target.localScale = to;
                return;
            }

            target.localScale = from;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // perf: ポップアップは一時停止中でも操作可能であるべきなので unscaledDeltaTime で進める
                elapsed += Time.unscaledDeltaTime;
                var easedRatio = Easing.Evaluate(easeType, Mathf.Clamp01(elapsed / duration));
                target.localScale = Vector3.LerpUnclamped(from, to, easedRatio);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            target.localScale = to;
        }

        /// <summary>
        /// CanvasGroup の alpha を from から to へ補間する。表示/非表示のフェード演出に使う。
        /// </summary>
        public static async UniTask FadeAsync(
            CanvasGroup target,
            float from,
            float to,
            float duration,
            EaseType easeType,
            CancellationToken cancellationToken)
        {
            if (duration <= 0f)
            {
                target.alpha = to;
                return;
            }

            target.alpha = from;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var easedRatio = Easing.Evaluate(easeType, Mathf.Clamp01(elapsed / duration));
                target.alpha = Mathf.LerpUnclamped(from, to, easedRatio);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            target.alpha = to;
        }
    }
}
