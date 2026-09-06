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

            // ScrollRect 上のボタンで、指を載せたままスワイプ（スクロール）して離した場合はタップとみなさない。
            // 押下と同じオブジェクト上で離しても、ドラッグが発生していれば _onDecide を発火させない。
            var pointerUpTarget = eventData.pointerCurrentRaycast.gameObject;
            if (!IsDrag(eventData) && _pointerDownTarget != null && _pointerDownTarget == pointerUpTarget)
            {
                _onDecide.OnNext(Unit.Default);
            }

            OnUp();
        }

        /// <summary>
        /// 押下から離すまでにスクロール（ドラッグ）が発生したかを判定する。
        /// EventSystem がドラッグ確定済み、または押下位置から pixelDragThreshold 以上動いていればドラッグとみなす。
        /// </summary>
        private static bool IsDrag(PointerEventData eventData)
        {
            if (eventData.dragging)
            {
                return true;
            }

            var threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10;
            return (eventData.position - eventData.pressPosition).sqrMagnitude > (float)threshold * threshold;
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