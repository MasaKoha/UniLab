using Cysharp.Threading.Tasks;

namespace UniLab.Popup
{
    public interface IPopupView
    {
        public void Initialize(IPopupParameter parameter);
        public UniTask OpenAsync();
        public UniTask WaitAsync();
        public void OnClose();
        public UniTask CloseAsync();
    }
}