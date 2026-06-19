using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 確認ポップアップを表示し応答を待つ Singleton マネージャ。ポップアップスタック基盤に統合する。
    /// </summary>
    public sealed class UniLabPopupManager : PopupManagerBase<UniLabPopupManager>, IPopupManager
    {
        [SerializeField] private ConfirmPopup _confirmPopupPrefab = null;

        /// <summary>
        /// ConfirmPopup を生成・表示してユーザー応答を待ち、最後に必ず閉じて破棄する。
        /// キャンセル・例外時も finally で後始末するため、View リークや HasActivePopup の取り残しが起きない。
        /// </summary>
        public async UniTask<PopupResult> ShowAsync(
            PopupParameter parameter,
            CancellationToken cancellationToken = default)
        {
            var popupInstance = InstantiatePopup(_confirmPopupPrefab, parameter);
            await OpenPopupAsync(popupInstance);

            try
            {
                return await popupInstance.GetResultAsync()
                    .AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                // 結果待ちがキャンセル/例外で抜けても、ここで確実に閉じてスタック・カウントを戻す
                await ClosePopupAsync(popupInstance);
            }
        }
    }
}
