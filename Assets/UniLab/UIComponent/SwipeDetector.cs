using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.UI
{
    public enum SwipeDirection
    {
        None,
        Left,
        Right,
        Up,
        Down
    }

    /// <summary>
    /// Detects swipe gestures on the Canvas and emits the direction via OnSwipe.
    /// Uses a platform-specific ISwipeInputReader to abstract mouse vs. touch input.
    /// Call Initialize() before use to start the update loop.
    /// </summary>
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
        private ISwipeInputReader _inputReader;

        public void Initialize()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            _inputReader = new MouseSwipeInputReader();
#else
            _inputReader = new TouchSwipeInputReader();
#endif
            Observable.EveryUpdate(destroyCancellationToken)
                .Subscribe(_ => UpdateSwipe());
        }

        private void UpdateSwipe()
        {
            _inputReader.Read(
                ref _startPos,
                ref _isTouching,
                pointerPosition => IsBlockedByHigherOrderCanvas(pointerPosition),
                (start, end) => DetectSwipe(start, end));
        }

        private void DetectSwipe(Vector2 start, Vector2 end)
        {
            var delta = end - start;

            if (delta.magnitude < _swipeThreshold)
            {
                // Ignore swipes shorter than the threshold (treated as a tap)
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

        // --- Input reader strategy ---

        /// <summary>
        /// Platform-specific input reading strategy. Implementations call onSwipeEnd when a drag gesture ends.
        /// </summary>
        private interface ISwipeInputReader
        {
            /// <summary>
            /// Called each frame. Implementations should call <paramref name="onSwipeEnd"/> with start/end positions
            /// when a gesture completes, after verifying it is not blocked via <paramref name="isBlocked"/>.
            /// </summary>
            void Read(
                ref Vector2 startPos,
                ref bool isTouching,
                System.Func<Vector2, bool> isBlocked,
                System.Action<Vector2, Vector2> onSwipeEnd);
        }

        private sealed class MouseSwipeInputReader : ISwipeInputReader
        {
            public void Read(
                ref Vector2 startPos,
                ref bool isTouching,
                System.Func<Vector2, bool> isBlocked,
                System.Action<Vector2, Vector2> onSwipeEnd)
            {
                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    if (isBlocked(UnityEngine.Input.mousePosition))
                    {
                        isTouching = false;
                        return;
                    }

                    startPos = UnityEngine.Input.mousePosition;
                    isTouching = true;
                    return;
                }

                if (isTouching && UnityEngine.Input.GetMouseButtonUp(0))
                {
                    isTouching = false;
                    onSwipeEnd(startPos, UnityEngine.Input.mousePosition);
                }
            }
        }

        private sealed class TouchSwipeInputReader : ISwipeInputReader
        {
            public void Read(
                ref Vector2 startPos,
                ref bool isTouching,
                System.Func<Vector2, bool> isBlocked,
                System.Action<Vector2, Vector2> onSwipeEnd)
            {
                if (UnityEngine.Input.touchCount <= 0)
                {
                    isTouching = false;
                    return;
                }

                var touch = UnityEngine.Input.GetTouch(0);
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        if (isBlocked(touch.position))
                        {
                            isTouching = false;
                            break;
                        }

                        startPos = touch.position;
                        isTouching = true;
                        break;
                    case TouchPhase.Ended:
                        if (isTouching)
                        {
                            isTouching = false;
                            onSwipeEnd(startPos, touch.position);
                        }

                        break;
                }
            }
        }
    }
}
