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
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastView : MonoBehaviour
    {
        // 画面下から滑り込ませる距離。トーストの高さにこの余白を足した分だけ下げてから戻す
        private const float SlideExtraDistancePixels = 32f;
        private const float SlideInDurationSeconds = 0.2f;
        private const float FadeOutDurationSeconds = 0.3f;

        [SerializeField] private TMP_Text _messageText = null;
        [SerializeField] private Image _backgroundImage = null;
        [SerializeField] private CanvasGroup _canvasGroup = null;

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
            var slideDistance = rectTransform.rect.height + SlideExtraDistancePixels;
            rectTransform.anchoredPosition = new Vector2(0f, -slideDistance);

            _canvasGroup.alpha = 1f;

            await rectTransform
                .DOAnchorPosY(0f, SlideInDurationSeconds)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: cancellationToken);

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(durationSeconds),
                cancellationToken: cancellationToken);

            // Fade out
            await _canvasGroup
                .DOFade(0f, FadeOutDurationSeconds)
                .SetEase(Ease.InCubic)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: cancellationToken);
        }
    }
}
