using R3;
using UniLab.UI;
using UnityEngine;

namespace UniLab.Popup
{
    public abstract class BackgroundBase : MonoBehaviour
    {
        [SerializeField] private UniButton _backgroundButton = null;

        private readonly Subject<Unit> _onClickBackgroundButton = new();
        public Observable<Unit> OnClickBackground;

        public void Initialize()
        {
            _backgroundButton.OnClick(() => { _onClickBackgroundButton.OnNext(default); });
            OnClickBackground = _onClickBackgroundButton;
            OnInitialize();
        }

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        protected abstract void OnInitialize();
    }
}