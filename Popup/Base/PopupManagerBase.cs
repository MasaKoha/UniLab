using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using ObservableCollections;
using R3;
using UniLab.Common;
using UnityEngine;

namespace UniLab.Popup
{
    public abstract class PopupManagerBase<T> : SingletonMonoBehaviour<T> where T : MonoBehaviour
    {
        public Transform PopupRoot { get; private set; }
        private readonly ObservableList<PopupBase> _popupList = new();
        private BackgroundBase _background;
        private bool _isActiveBackground;
        private Action<int> _onChangeListCount;

        public UniTask InitializeAsync(Transform root, BackgroundBase background)
        {
            PopupRoot = root;
            _background = background;
            SetEvent();
            return UniTask.CompletedTask;
        }

        private void SetEvent()
        {
            _onChangeListCount = count =>
            {
                if (count > 0)
                {
                    return;
                }

                if (!_isActiveBackground)
                {
                    return;
                }

                _background.SetActive(false);
                _isActiveBackground = false;
            };

            _background.OnClickBackground
                .Subscribe(OnClosePopup)
                .AddTo(this);
        }

        private async void OnClosePopup(Unit _)
        {
            await CloseCurrentPopupAsync();
        }

        public async UniTask OpenAsync(PopupBase popup)
        {
            if (!_isActiveBackground)
            {
                _background.SetActive(true);
                _isActiveBackground = true;
            }

            _popupList.Add(popup);
            _onChangeListCount.Invoke(_popupList.Count);
            await popup.InitializeAsync(UniLabUnique.GetUniqId());
            SetBackgroundSibling();
            await popup.OpenAsync();
            popup.OnClose.Subscribe(uniqId =>
            {
                var removePopup = _popupList.First(p => p.PopupUniqId == uniqId);
                _popupList.Remove(removePopup);
                _onChangeListCount.Invoke(_popupList.Count);
                SetBackgroundSibling();
            });
        }

        private void SetBackgroundSibling()
        {
            var childCount = PopupRoot.childCount;
            _background.transform.SetSiblingIndex(childCount - 2);
        }

        private async UniTask CloseCurrentPopupAsync()
        {
            var currentPopup = _popupList.Last();
            await currentPopup.CloseAsync();
        }
    }
}