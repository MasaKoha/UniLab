using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.UI
{
    public enum ButtonState
    {
        None,
        Up,
        Down,
        Hold
    }

    /// <summary>
    /// Extended Button that exposes hold, decide, and state-change observables.
    /// </summary>
    public class UniLabButton : Button
    {
        private readonly Subject<Unit> _onHold = new();
        private readonly Subject<Unit> _onDecide = new();
        private readonly BehaviorSubject<ButtonState> _stateSubject = new(ButtonState.Up);

        /// <summary>Fires once when the pointer is held down on this button.</summary>
        public Observable<Unit> OnHoldAsObservable() => _onHold;

        /// <summary>Fires when the pointer is released over the same object it was pressed on.</summary>
        public Observable<Unit> OnDecideAsObservable() => _onDecide;

        /// <summary>Emits the current ButtonState whenever it changes.</summary>
        public Observable<ButtonState> StateAsObservable() => _stateSubject;

        private GameObject _pointerDownTarget;

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _pointerDownTarget = eventData.pointerPressRaycast.gameObject;
            _stateSubject.OnNext(ButtonState.Down);
            _stateSubject.OnNext(ButtonState.Hold);
            _onHold.OnNext(Unit.Default);
            OnHold();
            OnDown();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            _stateSubject.OnNext(ButtonState.Up);

            var pointerUpTarget = eventData.pointerCurrentRaycast.gameObject;
            if (_pointerDownTarget != null && _pointerDownTarget == pointerUpTarget)
            {
                _onDecide.OnNext(Unit.Default);
            }

            OnUp();
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
            _onHold.Dispose();
            _onDecide.Dispose();
            _stateSubject.Dispose();
        }
    }
}