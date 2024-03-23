using Cysharp.Threading.Tasks;
using R3;
using UniLab.InputBlocking;
using UnityEngine;

namespace UniLab.Popup
{
    public abstract class PopupBase : MonoBehaviour
    {
        private bool _isClose;
        private readonly Subject<ulong> _onClose = new();
        public Observable<ulong> OnClose => _onClose;
        public ulong PopupUniqId { get; private set; }

        public async UniTask InitializeAsync(ulong popupUniqId)
        {
            PopupUniqId = popupUniqId;
            await OnInitializeAsync();
        }

        public async UniTask OpenAsync()
        {
            InputBlockingManager.Instance.Push(PopupUniqId);
            await OnOpenAsync();
            InputBlockingManager.Instance.Pop(PopupUniqId);
        }

        public async UniTask CloseAsync()
        {
            InputBlockingManager.Instance.Push(PopupUniqId);
            await OnCloseAsync();
            InputBlockingManager.Instance.Pop(PopupUniqId);
            _isClose = true;
            Destroy(gameObject);
        }

        public async UniTask WaitCloseAsync()
        {
            while (!_isClose)
            {
                await UniTask.NextFrame();
            }

            _onClose.OnNext(PopupUniqId);
            _onClose.Dispose();
        }

        protected abstract UniTask OnInitializeAsync();
        protected abstract UniTask OnOpenAsync();
        protected abstract UniTask OnCloseAsync();
    }
}