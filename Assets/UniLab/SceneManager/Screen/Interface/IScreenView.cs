namespace UniLab.Scene.Screen
{
    /// <summary>
    /// Represents the view contract for a screen managed by IScreenManager.
    /// </summary>
    public interface IScreenView
    {
        /// <summary>
        /// Makes the screen visible and active.
        /// </summary>
        void Show();

        /// <summary>
        /// Hides the screen from view.
        /// </summary>
        void Hide();
    }
}
