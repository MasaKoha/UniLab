using System;
using UniLab.Common;
using UniLab.UI;
using UnityEngine;

namespace UniLab.UI.Loading
{
    /// <summary>
    /// Singleton manager for a full-screen loading overlay.
    /// Uses a reference counter so nested Show() calls work correctly:
    /// the overlay only hides when all handles have been disposed.
    /// </summary>
    public class LoadingOverlayManager : SingletonMonoBehaviour<LoadingOverlayManager>, ILoadingOverlayManager
    {
        [SerializeField] private GameObject _overlayRoot = null;

        private int _showCount = 0;

        /// <summary>
        /// Increments the show counter, activates the overlay, and blocks input.
        /// Dispose the returned handle to decrement the counter and hide when it reaches zero.
        /// </summary>
        public IDisposable Show()
        {
            _showCount++;
            _overlayRoot.SetActive(true);

            // Hold an input block for the lifetime of this overlay handle
            var inputBlock = InputBlockManager.CreateInputBlockWithLoading();

            return new OverlayHandle(this, inputBlock);
        }

        private void Hide()
        {
            _showCount--;

            if (_showCount <= 0)
            {
                _showCount = 0;
                _overlayRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Disposable handle that decrements the overlay counter on dispose.
        /// </summary>
        private sealed class OverlayHandle : IDisposable
        {
            private readonly LoadingOverlayManager _manager;
            private readonly IDisposable _inputBlock;
            private bool _disposed = false;

            public OverlayHandle(LoadingOverlayManager manager, IDisposable inputBlock)
            {
                _manager = manager;
                _inputBlock = inputBlock;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _inputBlock.Dispose();
                _manager.Hide();
            }
        }
    }
}
