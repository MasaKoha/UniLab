using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Popup;
using UnityEngine;

namespace UniLab.Feature.UI.Dialog
{
    /// <summary>
    /// Singleton manager for showing modal dialogs and awaiting user responses.
    /// Wraps PopupManagerBase to integrate with the popup stack system.
    /// </summary>
    public class UniLabDialogManager : PopupManagerBase<UniLabDialogManager>, IDialogManager
    {
        [SerializeField] private DialogPopup _dialogPopupPrefab = null;

        /// <summary>
        /// Instantiates a DialogPopup, opens it, awaits the user's response, then destroys it.
        /// </summary>
        public async UniTask<DialogResult> ShowAsync(
            DialogParameter parameter,
            CancellationToken cancellationToken = default)
        {
            var popupInstance = InstantiatePopup(_dialogPopupPrefab, parameter);

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
