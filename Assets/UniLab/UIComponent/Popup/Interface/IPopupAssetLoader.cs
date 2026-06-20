using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// ポップアップ View の「ロード手段」を抽象化する。Resources / Addressables(AssetVault) 等の差異をここに閉じ込め、
    /// PopupViewProvider はこのインターフェースだけに依存する。Release で生成物とロードハンドルの双方を解放する。
    /// </summary>
    public interface IPopupAssetLoader
    {
        /// <summary>型 TPopup の View を parent 配下に生成し、非表示状態で返す。PopupViewProvider が表示前に呼ぶ。</summary>
        UniTask<TPopup> InstantiateAsync<TPopup>(Transform parent, CancellationToken cancellationToken)
            where TPopup : PopupBase;

        /// <summary>生成した View を解放する。生成物の破棄に加え、ロードハンドル / スコープも解放する。</summary>
        void Release(PopupBase popup);
    }
}
