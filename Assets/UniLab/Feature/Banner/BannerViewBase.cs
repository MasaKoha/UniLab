using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;

namespace UniLab.Feature.Banner
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

        // 現在表示されているバナーのパラメータリスト。このリストは元のパラメータリストからローテーションされて更新される。
        private List<TParameter> _currentBannerParameters;

        // 元のパラメータリスト。Initialize()で設定されたパラメータのコピー。ローテーションしても更新されないもの。
        private List<TParameter> _parametersOriginal;
        public int ParameterCount => _parametersOriginal.Count;

        public void Initialize(List<TParameter> parameters)
        {
            _parametersOriginal = new List<TParameter>(parameters);
            _currentBannerParameters = new List<TParameter>(parameters);
            foreach (var cell in _cells)
            {
                Destroy(cell.gameObject);
            }

            _cells.Clear();
            foreach (Transform child in _content)
            {
                Destroy(child.gameObject);
            }

            // ダミー: 末尾の前セル
            AddCell();

            // 実データ
            foreach (var _ in _parametersOriginal)
            {
                AddCell();
            }

            // ダミー: 先頭の次セル
            AddCell();
            UpdateCellPositions();
            InitializeCellContents();
            UpdateAllCellContents();
            // 0番目が中央
            _content.anchoredPosition = new Vector2(-(_cellWidth + _spaceX) * 0, 0f);
            OnInitialize();
            StartAutoScroll();
        }

        protected abstract void OnInitialize();

        private void StartAutoScroll()
        {
            StopAutoScroll();
            // 破棄済みや非アクティブ時は何もしない
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
            // 0: ダミー（末尾データ）
            if (_cells[0].GetComponent<BannerCellBase<TParameter>>() is { } dummyFirst)
            {
                dummyFirst.UpdateContentAsync(_currentBannerParameters[^1]).Forget();
            }

            // 1～N: 実データ
            for (var i = 0; i < _currentBannerParameters.Count; i++)
            {
                if (_cells[i + 1].GetComponent<BannerCellBase<TParameter>>() is { } cell)
                {
                    cell.UpdateContentAsync(_currentBannerParameters[i]).Forget();
                }
            }

            // N+1: ダミー（先頭データ）
            if (_cells[^1].GetComponent<BannerCellBase<TParameter>>() is { } dummyLast)
            {
                dummyLast.UpdateContentAsync(_currentBannerParameters[0]).Forget();
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
            // SetActive(false) の時でも動作するので、そのときはスクロールしないように修正
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
                var lastParam = _currentBannerParameters[^1];
                _currentBannerParameters.RemoveAt(_currentBannerParameters.Count - 1);
                _currentBannerParameters.Insert(0, lastParam);
            }
            else
            {
                var firstParam = _currentBannerParameters[0];
                _currentBannerParameters.RemoveAt(0);
                _currentBannerParameters.Add(firstParam);
            }

            UpdateAllCellContents();
            UpdateCellPositions();
            _content.anchoredPosition = new Vector2(-(_cellWidth + _spaceX) * 0, _content.anchoredPosition.y);
            // 中央に表示されているデータの「元リストでのインデックス」をセット
            var currentParameter = _currentBannerParameters[0];
            var originalIndex = _parametersOriginal.FindIndex(p => ReferenceEquals(p, currentParameter));
            _currentIndexReactiveProperty.Value = originalIndex;
            onComplete?.Invoke();
        }
    }
}