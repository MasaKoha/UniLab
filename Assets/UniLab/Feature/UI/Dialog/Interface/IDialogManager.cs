using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Popup;

namespace UniLab.Feature.UI.Dialog
{
    /// <summary>
    /// Manages dialog presentation and awaiting user confirmation/cancellation.
    /// </summary>
    public interface IDialogManager
    {
        /// <summary>
        /// Shows a dialog with the given parameters and waits for user response.
        /// </summary>
        UniTask<DialogResult> ShowAsync(DialogParameter parameter, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the result of a dialog interaction.
    /// </summary>
    public enum DialogResult
    {
        Confirm,
        Cancel,
    }

    /// <summary>
    /// Parameters for configuring dialog content and button visibility.
    /// Implements IPopupParameter so it can be passed directly to the popup stack system.
    /// </summary>
    public class DialogParameter : IPopupParameter
    {
        /// <summary>Title text displayed at the top of the dialog.</summary>
        public string Title { get; set; }

        /// <summary>Body message text of the dialog.</summary>
        public string Message { get; set; }

        /// <summary>Label for the confirm button.</summary>
        public string ConfirmLabel { get; set; } = "OK";

        /// <summary>Label for the cancel button. When null, the cancel button is hidden.</summary>
        public string CancelLabel { get; set; }

        // Back key dismisses the dialog as Cancel
        bool IPopupParameter.EnableBackKey => true;
        Func<UniTask> IPopupParameter.CustomBackAsync => null;

        // Background tap should not close a dialog to prevent accidental dismissal
        bool IPopupParameter.EnableBackgroundClose => false;
    }
}
