using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.Common;
using UnityEngine;

namespace UniLab.UI.Popup
{
    public abstract class PopupManagerBase<T> : SingletonMonoBehaviour<T> where T : MonoBehaviour
    {
        [SerializeField] private Transform _popupRoot = null;
        private readonly ReactiveProperty<int> _popupCount = new();
        private readonly Stack<PopupBase> _popupStack = new();
        public bool HasActivePopup => _popupCount.Value > 0;

        protected override void OnAwake()
        {
            _popupCount.Subscribe(_ =>
                {
                    foreach (var popup in _popupStack)
                    {
                        popup.SetBackgroundButtonActiveIfTop(_popupStack);
                    }
                })
                .AddTo(destroyCancellationToken);
        }

        public TPopup InstantiatePopup<TPopup>(TPopup popup, IPopupParameter parameter) where TPopup : PopupBase
        {
            if (popup == null)
            {
                throw new System.ArgumentNullException(nameof(popup), "Popup cannot be null.");
            }

            var popupObject = Instantiate(popup, _popupRoot);
            popupObject.Initialize(parameter);
            popupObject.gameObject.SetActive(false);
            return popupObject;
        }

        public async UniTask OpenPopupAsync<TPopup>(TPopup popupInstance) where TPopup : PopupBase
        {
            _popupStack.Push(popupInstance);
            _popupCount.Value++;
            popupInstance.gameObject.SetActive(true);
            await popupInstance.OpenAsync();
        }

        public async UniTask WaitPopupAsync<TPopup>(TPopup popupInstance, bool destroy = true) where TPopup : PopupBase
        {
            await popupInstance.WaitAsync();
            _ = _popupStack.Pop();
            if (destroy)
            {
                Destroy(popupInstance.gameObject);
            }

            _popupCount.Value--;
        }

        public async UniTask CloseTopPopupAsync()
        {
            var popupInstance = _popupStack.Peek();
            var parameter = popupInstance.Parameter;
            var parameterCustomBackAsync = parameter.CustomBackAsync;
            if (!parameter.EnableBackKey)
            {
                return;
            }

            if (parameterCustomBackAsync != null)
            {
                await parameterCustomBackAsync();
                return;
            }

            popupInstance.OnClose();
        }
    }
}
