using Cysharp.Threading.Tasks;
using R3;
using UniLab.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.Banner
{
    public abstract class BannerCellBase<T> : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerClickHandler where T : class
    {
        [SerializeField] private Image _targetImage = null;
        /// <summary>セル自身の領域として使う RectTransform を結線する。</summary>
        [SerializeField] private RectTransform _rectTransform = null;
        protected Image TargetImage => _targetImage;
        internal RectTransform RectTransform => _rectTransform;
        private readonly Subject<SwipeDirection> _onSwipe = new();
        public Observable<SwipeDirection> OnSwipe => _onSwipe;
        private readonly Subject<Unit> _onClick = new();
        public Observable<Unit> OnClicked => _onClick;
        private Vector2 _dragStartPos;
        private bool _isDrag;
        public abstract void Initialize();
        public abstract UniTask UpdateContentAsync(T parameter);

        public void OnDrag(PointerEventData eventData)
        {
            var deltaX = eventData.position.x - _dragStartPos.x;
            if (Mathf.Abs(deltaX) > 30f)
            {
                _isDrag = true;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragStartPos = eventData.position;
            _isDrag = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var deltaX = eventData.position.x - _dragStartPos.x;
            if (Mathf.Abs(deltaX) > 30f)
            {
                _onSwipe.OnNext(deltaX > 0 ? SwipeDirection.Left : SwipeDirection.Right);
            }

            _isDrag = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isDrag)
            {
                return;
            }

            _onClick.OnNext(Unit.Default);
        }
    }
}
