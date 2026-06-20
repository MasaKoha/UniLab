using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// IPopupAssetLoader にロードを委譲する汎用 ViewProvider。生成先 PopupRoot を保持し、
    /// ローダーを差し替えるだけで Resources / Addressables 等の供給手段を切り替えられる。
    /// </summary>
    public sealed class PopupViewProvider : IPopupViewProvider
    {
        private readonly IPopupAssetLoader _loader;
        private readonly Transform _popupRoot;

        /// <summary>ロード手段と生成先ルートを注入する。</summary>
        public PopupViewProvider(IPopupAssetLoader loader, Transform popupRoot)
        {
            _loader = loader;
            _popupRoot = popupRoot;
        }

        /// <summary>ローダーに委譲して View を生成する。</summary>
        public UniTask<TPopup> LoadAsync<TPopup>(CancellationToken cancellationToken) where TPopup : PopupBase
        {
            return _loader.InstantiateAsync<TPopup>(_popupRoot, cancellationToken);
        }

        /// <summary>ローダーに委譲して View を解放する。</summary>
        public void Release(PopupBase popup)
        {
            _loader.Release(popup);
        }
    }
}
