using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using UniLab.UI;
using UnityEngine;

namespace UniLab.Banner
{
    public abstract class BannerViewBase<TPrefab, TParameter> : MonoBehaviour where TPrefab : MonoBehaviour
        where TParameter : class
    {
        [SerializeField] private TPrefab _bannerCellPrefab = null;
        [SerializeField] private RectTransform _content = null;
        [SerializeField] private float _spaceX = 96f;
        [SerializeField] private float _autoScrollInterval = 3.0f;
        private CancellationTokenSource _autoScrollCts;
        private readonly ReactiveProperty<int> _currentIndexReactiveProperty = new(0);
        public Observable<int> OnCurrentIndexChanged => _currentIndexReactiveProperty;
        private readonly List<RectTransform> _cells = new();

        private float _cellWidth;

        // perf: LinkedList enables O(1) rotation (RemoveFirst/AddLast and vice versa) vs O(N) RemoveAt(0) on List.
        private LinkedList<TParameter> _currentBannerParameters;

        // Original unrotated parameter list. Never mutated after Initialize().
        private List<TParameter> _parametersOriginal;
        public int ParameterCount => _parametersOriginal.Count;

        public void Initialize(List<TParameter> parameters)
        {
            _parametersOriginal = new List<TParameter>(parameters);
            _currentBannerParameters = new LinkedList<TParameter>(parameters);
            foreach (var cell in _cells)
            {
                Destroy(cell.gameObject);
            }

            _cells.Clear();
            foreach (Transform child in _content)
            {
                Destroy(child.gameObject);
            }

            // Dummy: cell before the last
            AddCell();

            // Real data
            foreach (var _ in _parametersOriginal)
            {
                AddCell();
            }

            // Dummy: cell after the first
            AddCell();
            UpdateCellPositions();
            InitializeCellContents();
            UpdateAllCellContents();
            // Index 0 is centered
            _content.anchoredPosition = new Vector2(-(_cellWidth + _spaceX) * 0, 0f);
            OnInitialize();
            StartAutoScroll();
        }

        protected abstract void OnInitialize();

        private void StartAutoScroll()
        {
            StopAutoScroll();
            // Do nothing when destroyed or inactive
            if (this == null || !this.gameObject.activeInHierarchy)
            {
                return;
            }

            _autoScrollCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy()
            );
            AutoScrollLoop(_autoScrollCts.Token).Forget();
        }

        protected virtual void OnEnable()
        {
            StartAutoScroll();
        }

        protected virtual void OnDisable()
        {
            StopAutoScroll();
        }

        private void StopAutoScroll()
        {
            _autoScrollCts?.Cancel();
            _autoScrollCts?.Dispose();
            _autoScrollCts = null;
        }

        private async UniTask AutoScrollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(_autoScrollInterval), cancellationToken: token);
                if (!token.IsCancellationRequested)
                {
                    OnCellSwiped(SwipeDirection.Right);
                }
            }
        }

        private void AddCell()
        {
            var cell = Instantiate(_bannerCellPrefab, _content);
            var rectTransform = cell.GetComponent<RectTransform>();
            if (_cellWidth == 0f)
            {
                _cellWidth = rectTransform.rect.width;
            }

            _cells.Add(rectTransform);

            if (cell is BannerCellBase<TParameter> bannerCell)
            {
                bannerCell.OnSwipe
                    .Subscribe(OnCellSwiped)
                    .AddTo(this);
            }
        }

        private void UpdateCellPositions()
        {
            const int centerIndex = 1;
            for (var i = 0; i < _cells.Count; i++)
            {
                var posX = (i - centerIndex) * (_cellWidth + _spaceX);
                _cells[i].anchoredPosition = new Vector2(posX, 0f);
            }
        }

        public void InitializeCellContents()
        {
            foreach (var cell in _cells)
            {
                var banner = cell.GetComponent<BannerCellBase<TParameter>>();
                banner.Initialize();
            }
        }

        private void UpdateAllCellContents()
        {
            // 0: dummy cell (shows the last parameter — appears to the left of the first real cell)
            if (_cells[0].GetComponent<BannerCellBase<TParameter>>() is { } dummyFirst)
            {
                dummyFirst.UpdateContentAsync(_currentBannerParameters.Last.Value).Forget();
            }

            // 1～N: real data cells
            var cellIndex = 1;
            foreach (var parameter in _currentBannerParameters)
            {
                if (_cells[cellIndex].GetComponent<BannerCellBase<TParameter>>() is { } cell)
                {
                    cell.UpdateContentAsync(parameter).Forget();
                }

                cellIndex++;
            }

            // N+1: dummy cell (shows the first parameter — appears to the right of the last real cell)
            if (_cells[^1].GetComponent<BannerCellBase<TParameter>>() is { } dummyLast)
            {
                dummyLast.UpdateContentAsync(_currentBannerParameters.First.Value).Forget();
            }
        }

        private void OnCellSwiped(SwipeDirection direction)
        {
            StartAutoScroll();
            if (_cells.Count == 0)
            {
                return;
            }

            var moveIndex = direction == SwipeDirection.Left ? 1 : -1;
            SnapToIndex(moveIndex, direction).Forget();
        }

        private async UniTask SnapToIndex(
            int index,
            SwipeDirection direction,
            float duration = 0.3f,
            Ease ease = Ease.OutQuart,
            System.Action onComplete = null)
        {
            // Skip scrolling when the content is inactive
            if (!_content.gameObject.activeInHierarchy)
            {
                return;
            }

            var targetX = _content.anchoredPosition.x + (_cellWidth + _spaceX) * (direction == SwipeDirection.Left ? 1 : -1);
            await _content.DOAnchorPosX(targetX, duration)
                .SetEase(ease)
                .ToUniTask();

            if (direction == SwipeDirection.Left)
            {
                // perf: O(1) rotate-right — move last node to front
                var lastValue = _currentBannerParameters.Last.Value;
                _currentBannerParameters.RemoveLast();
                _currentBannerParameters.AddFirst(lastValue);
            }
            else
            {
                // perf: O(1) rotate-left — move first node to back
                var firstValue = _currentBannerParameters.First.Value;
                _currentBannerParameters.RemoveFirst();
                _currentBannerParameters.AddLast(firstValue);
            }

            UpdateAllCellContents();
            UpdateCellPositions();
            _content.anchoredPosition = new Vector2(-(_cellWidth + _spaceX) * 0, _content.anchoredPosition.y);
            // Find the original index of the parameter now at the front (center cell).
            var currentParameter = _currentBannerParameters.First.Value;
            var originalIndex = _parametersOriginal.FindIndex(p => ReferenceEquals(p, currentParameter));
            _currentIndexReactiveProperty.Value = originalIndex;
            onComplete?.Invoke();
        }
    }
}
