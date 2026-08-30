using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Toast
{
    /// <summary>
    /// トースト通知の表示。同時に出すのは1件で、表示中に Show() を呼ぶと今のものを取り消して差し替える。
    /// 常駐オブジェクトに載せ、利用側の LifetimeScope で <see cref="IToastManager"/> として登録する。
    /// </summary>
    public class ToastManager : MonoBehaviour, IToastManager
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

        private void OnDestroy()
        {
            CancelCurrentToast();
        }
    }
}
