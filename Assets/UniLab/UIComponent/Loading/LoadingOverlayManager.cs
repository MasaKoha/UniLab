using System;
using UnityEngine;

namespace UniLab.UI.Loading
{
    /// <summary>
    /// 全画面ローディングオーバーレイ。参照カウントで入れ子の Show() に対応し、全ハンドルが Dispose されたときだけ隠す。
    /// 常駐オブジェクトに載せ、利用側の LifetimeScope で <see cref="ILoadingOverlayManager"/> として登録する。
    /// </summary>
    public class LoadingOverlayManager : MonoBehaviour, ILoadingOverlayManager
    {
        [SerializeField] private GameObject _overlayRoot = null;

        private int _showCount = 0;
        private IInputBlockManager _inputBlockManager;

        /// <summary>入力ブロックの発行元を受け取る。所有者が起動時に一度だけ呼ぶ。</summary>
        public void Initialize(IInputBlockManager inputBlockManager)
        {
            _inputBlockManager = inputBlockManager;
        }

        /// <summary>
        /// Increments the show counter, activates the overlay, and blocks input.
        /// Dispose the returned handle to decrement the counter and hide when it reaches zero.
        /// </summary>
        public IDisposable Show()
        {
            _showCount++;
            _overlayRoot.SetActive(true);

            // Hold an input block for the lifetime of this overlay handle
            var inputBlock = _inputBlockManager.CreateInputBlockWithLoading();

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
