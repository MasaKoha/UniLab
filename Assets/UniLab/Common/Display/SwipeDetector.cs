using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.Common.Display
{
    public enum SwipeDirection
    {
        None,
        Left,
        Right,
        Up,
        Down
    }

    public class SwipeDetector : MonoBehaviour
    {
        [SerializeField] private GraphicRaycaster _raycaster = null;
        [SerializeField] private Canvas _raycasterCanvas = null;
        [SerializeField] private EventSystem _eventSystem = null;
        [SerializeField] private float _swipeThreshold = 300;

        private Vector2 _startPos;
        private bool _isTouching;

        private readonly Subject<SwipeDirection> _onSwipe = new();
        public Observable<SwipeDirection> OnSwipe => _onSwipe;

        private readonly List<RaycastResult> _raycastResults = new();

        public void Initialize()
        {
            Observable.EveryUpdate(destroyCancellationToken)
                .Subscribe(_ => UpdateSwipe());
        }

        private void UpdateSwipe()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                if (IsBlockedByHigherOrderCanvas(UnityEngine.Input.mousePosition))
                {
                    _isTouching = false;
                    return;
                }

                _startPos = UnityEngine.Input.mousePosition;
                _isTouching = true;
                return;
            }

            if (_isTouching && UnityEngine.Input.GetMouseButtonUp(0))
            {
                _isTouching = false;
                DetectSwipe(_startPos, UnityEngine.Input.mousePosition);
            }
#else
            if (UnityEngine.Input.touchCount <= 0)
            {
                _isTouching = false;
                return;
            }

            var touch = UnityEngine.Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (IsBlockedByHigherOrderCanvas(touch.position))
                    {
                        _isTouching = false;
                        break;
                    }

                    _startPos = touch.position;
                    _isTouching = true;
                    break;
                case TouchPhase.Ended:
                    if (_isTouching)
                    {
                        _isTouching = false;
                        DetectSwipe(_startPos, touch.position);
                    }
                    break;
            }
#endif
        }

        private void DetectSwipe(Vector2 start, Vector2 end)
        {
            var delta = end - start;

            if (delta.magnitude < _swipeThreshold)
            {
                // スワイプ距離がしきい値未満なら何もしない（タップ扱い、ボタン決定は基盤側Button側で判定される）
                return;
            }

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                _onSwipe.OnNext(delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left);
            }
            else
            {
                _onSwipe.OnNext(delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down);
            }
        }

        private bool IsBlockedByHigherOrderCanvas(Vector2 pointerPosition)
        {
            var canvas = ResolveCanvas();
            if (canvas == null)
            {
                return false;
            }

            var eventSystem = ResolveEventSystem();
            if (eventSystem == null)
            {
                return false;
            }

            var pointerData = new PointerEventData(eventSystem)
            {
                position = pointerPosition
            };

            _raycastResults.Clear();
            eventSystem.RaycastAll(pointerData, _raycastResults);
            if (_raycastResults.Count == 0)
            {
                return false;
            }

            var baseLayerValue = SortingLayer.GetLayerValueFromID(canvas.sortingLayerID);
            var baseSortingOrder = canvas.sortingOrder;

            foreach (var raycastResult in _raycastResults)
            {
                var targetCanvas = GetCanvasFromResult(raycastResult);
                if (targetCanvas == null || targetCanvas == canvas)
                {
                    continue;
                }

                var targetLayerValue = SortingLayer.GetLayerValueFromID(targetCanvas.sortingLayerID);
                if (targetLayerValue > baseLayerValue)
                {
                    return true;
                }

                if (targetLayerValue == baseLayerValue && targetCanvas.sortingOrder > baseSortingOrder)
                {
                    return true;
                }
            }

            return false;
        }

        private Canvas ResolveCanvas()
        {
            if (_raycasterCanvas != null || _raycaster != null)
            {
                return _raycasterCanvas;
            }

            _raycasterCanvas = GetComponentInParent<Canvas>();
            return _raycasterCanvas;
        }

        private EventSystem ResolveEventSystem()
        {
            return _eventSystem != null ? _eventSystem : EventSystem.current;
        }

        private static Canvas GetCanvasFromResult(RaycastResult result)
        {
            return result.gameObject == null ? null : result.gameObject.GetComponentInParent<Canvas>();
        }
    }
}