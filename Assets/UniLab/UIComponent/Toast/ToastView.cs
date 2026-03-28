using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Toast
{
    /// <summary>
    /// View component for a single toast notification.
    /// Handles slide-in, display duration, and fade-out animation.
    /// </summary>
    public class ToastView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _messageText = null;
        [SerializeField] private Image _backgroundImage = null;

        /// <summary>
        /// Animates the toast: slide in from bottom, hold, then fade out.
        /// Cancellation at any point will complete the task without error.
        /// </summary>
        public async UniTask ShowAsync(
            string message,
            Color backgroundColor,
            float durationSeconds,
            CancellationToken cancellationToken)
        {
            _messageText.text = message;
            _backgroundImage.color = backgroundColor;

            // Slide in from below
            var rectTransform = (RectTransform)transform;
            var slideDistance = rectTransform.rect.height + 32f;
            rectTransform.anchoredPosition = new Vector2(0f, -slideDistance);

            var canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            await rectTransform
                .DOAnchorPosY(0f, 0.2f)
                .SetEase(Ease.OutCubic)
                .ToUniTask(cancellationToken: cancellationToken);

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(durationSeconds),
                cancellationToken: cancellationToken);

            // Fade out
            await canvasGroup
                .DOFade(0f, 0.3f)
                .SetEase(Ease.InCubic)
                .ToUniTask(cancellationToken: cancellationToken);
        }
    }
}
