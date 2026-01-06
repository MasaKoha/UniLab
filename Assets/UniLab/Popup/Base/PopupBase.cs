using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.Popup
{
    public abstract class PopupBase : MonoBehaviour, IPopupView
    {
        [SerializeField] private Button _backgroundButton = null;
        public IPopupParameter Parameter { get; private set; }

        public void Initialize(IPopupParameter parameter)
        {
            Parameter = parameter;
            SetEvent();
            OnInitialize();
        }

        private void SetEvent()
        {
            _backgroundButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    if (!Parameter.EnableBackgroundClose)
                    {
                        return;
                    }

                    OnClose();
                })
                .AddTo(this);
        }

        public void SetBackgroundButtonActiveIfTop(Stack<PopupBase> popupStack)
        {
            // スタックが空でない & 一番上が自分なら true
            if (popupStack.Count <= 0 || popupStack.Peek() != this)
            {
                _backgroundButton.gameObject.SetActive(false);
                return;
            }

            _backgroundButton.gameObject.SetActive(true);
        }

        public void SetActiveBackground(bool isActive)
        {
            _backgroundButton.gameObject.SetActive(isActive);
        }

        protected abstract void OnInitialize();
        public abstract UniTask OpenAsync();
        public abstract UniTask WaitAsync();
        public abstract void OnClose();
        public abstract UniTask CloseAsync();
    }
}