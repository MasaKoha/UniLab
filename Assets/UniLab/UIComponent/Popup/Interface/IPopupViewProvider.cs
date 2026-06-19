using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// ポップアップ View の入手元を抽象化する。Popup 基盤は Addressables 等の具体的な供給手段を知らない。
    /// </summary>
    public interface IPopupViewProvider
    {
        /// <summary>型 TPopup の View を生成/ロードし、非表示状態で返す。PopupService が表示前に呼ぶ。</summary>
        UniTask<TPopup> LoadAsync<TPopup>(CancellationToken cancellationToken) where TPopup : PopupBase;

        /// <summary>表示を終えた View を解放する。生成元に応じて破棄またはプール返却する。</summary>
        void Release(PopupBase popup);
    }
}
