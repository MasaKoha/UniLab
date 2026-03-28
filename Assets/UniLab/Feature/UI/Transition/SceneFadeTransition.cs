using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace UniLab.Feature.UI.Transition
{
    /// <summary>
    /// MonoBehaviour that drives fullscreen fade transitions via a CanvasGroup.
    /// Attach to a Canvas with a full-screen black Image and a CanvasGroup component.
    /// Called from UniLabSceneManagerBase.FadeInAsync / FadeOutAsync.
    /// </summary>
    public class SceneFadeTransition : MonoBehaviour, ISceneTransition
    {
        [SerializeField] private CanvasGroup _canvasGroup = null;

        /// <summary>
        /// Fades alpha from 1 to 0, revealing the scene.
        /// Disables raycast blocking after the fade completes so the UI becomes interactive.
        /// </summary>
        public async UniTask FadeInAsync(float duration = 0.3f, CancellationToken cancellationToken = default)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;

            await FadeAsync(0f, duration, cancellationToken);

            // Unblock input only after fully faded in to prevent premature interaction
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Fades alpha from 0 to 1, darkening the screen before a scene load.
        /// Enables raycast blocking immediately to prevent input during transition.
        /// </summary>
        public async UniTask FadeOutAsync(float duration = 0.3f, CancellationToken cancellationToken = default)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;

            await FadeAsync(1f, duration, cancellationToken);
        }

        private async UniTask FadeAsync(float targetAlpha, float duration, CancellationToken cancellationToken)
        {
            var tween = _canvasGroup
                .DOFade(targetAlpha, duration)
                .SetEase(Ease.Linear);

            try
            {
                await tween.ToUniTask(cancellationToken: cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                // Kill the tween to avoid dangling animations if cancelled externally
                tween.Kill();
                throw;
            }
        }
    }
}
