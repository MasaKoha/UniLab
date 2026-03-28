using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// Manages confirmation popup presentation and awaiting user confirmation/cancellation.
    /// </summary>
    public interface IPopupManager
    {
        /// <summary>
        /// Shows a confirmation popup with the given parameters and waits for user response.
        /// </summary>
        UniTask<PopupResult> ShowAsync(PopupParameter parameter, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the result of a confirmation popup interaction.
    /// </summary>
    public enum PopupResult
    {
        Confirm,
        Cancel,
    }

    /// <summary>
    /// Parameters for configuring confirmation popup content and button visibility.
    /// Implements IPopupParameter so it can be passed directly to the popup stack system.
    /// </summary>
    public class PopupParameter : IPopupParameter
    {
        /// <summary>Title text displayed at the top of the popup.</summary>
        public string Title { get; set; }

        /// <summary>Body message text of the popup.</summary>
        public string Message { get; set; }

        /// <summary>Label for the confirm button.</summary>
        public string ConfirmLabel { get; set; } = "OK";

        /// <summary>Label for the cancel button. When null, the cancel button is hidden.</summary>
        public string CancelLabel { get; set; }

        // Back key dismisses the popup as Cancel
        bool IPopupParameter.EnableBackKey => true;
        Func<UniTask> IPopupParameter.CustomBackAsync => null;

        // Background tap should not close a confirmation popup to prevent accidental dismissal
        bool IPopupParameter.EnableBackgroundClose => false;
    }
}
