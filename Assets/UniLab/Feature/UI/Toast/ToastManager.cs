using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Common;
using UnityEngine;

namespace UniLab.Feature.UI.Toast
{
    /// <summary>
    /// Singleton manager for displaying toast notifications.
    /// Only one toast is shown at a time; calling Show() while a toast is visible
    /// cancels the current one before displaying the new one.
    /// </summary>
    public class ToastManager : SingletonMonoBehaviour<ToastManager>, IToastManager
    {
        [SerializeField] private RectTransform _toastRoot = null;
        [SerializeField] private ToastView _toastPrefab = null;

        // Index matches ToastType enum order: Info, Success, Warning, Error
        [SerializeField] private Color[] _typeColors = new Color[]
        {
            new Color(0.2f, 0.2f, 0.2f, 1f),   // Info: dark gray
            new Color(0.18f, 0.64f, 0.32f, 1f), // Success: green
            new Color(0.87f, 0.62f, 0.12f, 1f), // Warning: amber
            new Color(0.83f, 0.18f, 0.18f, 1f), // Error: red
        };

        private CancellationTokenSource _currentToastCts;

        /// <summary>
        /// Shows a toast notification. Cancels any currently displayed toast first.
        /// </summary>
        public void Show(string message, ToastType type = ToastType.Info, float durationSeconds = 2f)
        {
            CancelCurrentToast();
            _currentToastCts = new CancellationTokenSource();
            ShowInternalAsync(message, type, durationSeconds, _currentToastCts.Token).Forget();
        }

        private async UniTaskVoid ShowInternalAsync(
            string message,
            ToastType type,
            float durationSeconds,
            CancellationToken cancellationToken)
        {
            var toastInstance = Instantiate(_toastPrefab, _toastRoot);
            var backgroundColor = _typeColors[(int)type];

            try
            {
                await toastInstance.ShowAsync(message, backgroundColor, durationSeconds, cancellationToken);
            }
            finally
            {
                // Destroy regardless of cancellation to avoid orphaned GameObjects
                if (toastInstance != null)
                {
                    Destroy(toastInstance.gameObject);
                }
            }
        }

        private void CancelCurrentToast()
        {
            if (_currentToastCts == null)
            {
                return;
            }

            _currentToastCts.Cancel();
            _currentToastCts.Dispose();
            _currentToastCts = null;
        }

        protected override void OnDispose()
        {
            CancelCurrentToast();
        }
    }
}
