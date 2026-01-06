using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using R3;
using Cysharp.Threading.Tasks;

namespace UniLab.UI
{
    public enum ButtonState
    {
        None,
        Up,
        Down,
        Hold
    }

    public class UniLabButton : Button
    {
        private readonly Subject<Unit> _onHold = new();
        private readonly Subject<Unit> _onDecide = new();
        private readonly BehaviorSubject<ButtonState> _stateSubject = new(ButtonState.Up);

        public Observable<Unit> OnHoldAsObservable() => _onHold;
        public Observable<Unit> OnDecideAsObservable() => _onDecide;
        public Observable<ButtonState> StateAsObservable() => _stateSubject;

        private GameObject _pointerDownTarget;
        private CancellationTokenSource _holdCts;

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _pointerDownTarget = eventData.pointerPressRaycast.gameObject;
            _stateSubject.OnNext(ButtonState.Down);
            _holdCts = new CancellationTokenSource();
            HoldOnceAsync(_holdCts.Token).Forget();
            OnDown();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            _holdCts?.Cancel();
            _holdCts = null;
            _stateSubject.OnNext(ButtonState.Up);

            var pointerUpTarget = eventData.pointerCurrentRaycast.gameObject;
            if (_pointerDownTarget != null && _pointerDownTarget == pointerUpTarget)
            {
                _onDecide.OnNext(Unit.Default);
            }

            OnUp();
        }

        // 最初の1回だけ Hold を発火し、状態を Hold にする
        private async UniTask HoldOnceAsync(CancellationToken token)
        {
            if (!token.IsCancellationRequested)
            {
                _stateSubject.OnNext(ButtonState.Hold);
                _onHold.OnNext(Unit.Default);
            }

            OnHold();
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// ポインターダウン時の処理
        /// </summary>
        protected virtual void OnDown()
        {
        }

        /// <summary>
        /// ポインターアップ時の処理
        /// </summary>
        protected virtual void OnUp()
        {
        }

        /// <summary>
        /// ポインターホールド時の処理
        /// </summary>
        protected virtual void OnHold()
        {
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _holdCts?.Cancel();
            _onHold.Dispose();
            _onDecide.Dispose();
            _stateSubject.Dispose();
        }
    }
}