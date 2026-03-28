namespace UniLab.UI.Toast
{
    /// <summary>
    /// Manages transient toast notification display.
    /// </summary>
    public interface IToastManager
    {
        /// <summary>
        /// Shows a toast notification. If a toast is already visible it is cancelled immediately.
        /// </summary>
        void Show(string message, ToastType type = ToastType.Info, float durationSeconds = 2f);
    }

    /// <summary>
    /// Semantic category of a toast notification, used to determine background color.
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
    }
}
