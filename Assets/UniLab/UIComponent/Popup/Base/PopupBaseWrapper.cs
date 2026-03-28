namespace UniLab.UI.Popup
{
    public abstract class PopupBaseWrapper<TParameter> : PopupBase where TParameter : IPopupParameter
    {
        public new TParameter Parameter => (TParameter)base.Parameter;
    }
}
