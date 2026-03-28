using System;

namespace UniLab.Feature.UI.Loading
{
    /// <summary>
    /// Manages a full-screen loading overlay with reference-counted show/hide.
    /// </summary>
    public interface ILoadingOverlayManager
    {
        /// <summary>
        /// Increments the show counter and displays the overlay.
        /// The overlay hides automatically when the returned IDisposable is disposed.
        /// Usage: using (loadingOverlayManager.Show()) { await heavyOperation(); }
        /// </summary>
        IDisposable Show();
    }
}
