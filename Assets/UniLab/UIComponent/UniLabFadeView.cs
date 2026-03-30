using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// MonoBehaviour base that drives show/hide transitions via a CanvasGroup.
    /// Show/Hide provide instant visibility control; ShowAsync/HideAsync add DOTween fade.
    /// Override these methods in derived classes to customize animation behaviour.
    /// </summary>
    public class UniLabFadeView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        /// <summary>Makes the view instantly visible and activates the GameObject.</summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
        }

        /// <summary>Makes the view instantly invisible and deactivates the GameObject.</summary>
        public virtual void Hide()
        {
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Activates the GameObject and fades alpha 0 → 1 over <paramref name="duration"/> seconds.
        /// </summary>
        public virtual async UniTask ShowAsync(float duration = 0.3f, CancellationToken cancellationToken = default)
        {
            gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;

            var tween = _canvasGroup.DOFade(1f, duration).SetEase(Ease.Linear);
            try
            {
                await tween.ToUniTask(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            finally
            {
                tween.Kill();
            }
        }

        /// <summary>
        /// Fades alpha 1 → 0 over <paramref name="duration"/> seconds, then deactivates the GameObject.
        /// The GameObject is not deactivated if the operation is cancelled.
        /// </summary>
        public virtual async UniTask HideAsync(float duration = 0.3f, CancellationToken cancellationToken = default)
        {
            var tween = _canvasGroup.DOFade(0f, duration).SetEase(Ease.Linear);
            try
            {
                await tween.ToUniTask(cancellationToken: cancellationToken);
                gameObject.SetActive(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            finally
            {
                tween.Kill();
            }
        }
    }
}
