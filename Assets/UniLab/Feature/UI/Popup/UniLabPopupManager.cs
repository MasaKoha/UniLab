using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Popup;
using UnityEngine;

namespace UniLab.Feature.UI.Popup
{
    /// <summary>
    /// Singleton manager for showing modal confirmation popups and awaiting user responses.
    /// Wraps PopupManagerBase to integrate with the popup stack system.
    /// </summary>
    public class UniLabPopupManager : PopupManagerBase<UniLabPopupManager>, IPopupManager
    {
        [SerializeField] private ConfirmPopup _confirmPopupPrefab = null;

        /// <summary>
        /// Instantiates a ConfirmPopup, opens it, awaits the user's response, then destroys it.
        /// </summary>
        public async UniTask<PopupResult> ShowAsync(
            PopupParameter parameter,
            CancellationToken cancellationToken = default)
        {
            var popupInstance = InstantiatePopup(_confirmPopupPrefab, parameter);

            // Subscribe to result before OpenPopupAsync to avoid missing the event
            var resultTask = popupInstance.GetResultAsync()
                .AttachExternalCancellation(cancellationToken);

            await OpenPopupAsync(popupInstance);

            var result = await resultTask;

            await WaitPopupAsync(popupInstance);

            return result;
        }
    }
}
