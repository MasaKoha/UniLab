namespace UniLab.Popup
{
    public sealed class GlobalPopupManager : PopupManagerBase<GlobalPopupManager>
    {
        public void Initialize()
        {
            SetDonDestroyOnLoad();
        }
    }
}