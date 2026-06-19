using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup.Sample
{
    /// <summary>
    /// Popup 基盤 v2 の表示、優先度制御、入力ブロックと v1 後方互換を確認する起点。
    /// </summary>
    public sealed class PopupSampleEntry : MonoBehaviour
    {
        private const int RewardAmount = 100;
        private const int PriorityRewardAmount = 1;

        [SerializeField] private SerializeFieldPopupViewProvider _viewProvider = null;
        [SerializeField] private UniLabPopupManager _legacyManager = null;
        [SerializeField] private Button _confirmButton = null;
        [SerializeField] private Button _rewardButton = null;
        [SerializeField] private Button _priorityTestButton = null;
        [SerializeField] private Button _legacyButton = null;
        [SerializeField] private CanvasGroup _buttonGroup = null;

        private IPopupService _popupService = null;
        private readonly CompositeDisposable _disposables = new();

        private void Awake()
        {
            _popupService = new PopupService(_viewProvider);
        }

        private void Start()
        {
            _popupService.HasActivePopup
                .Subscribe(isActive => _buttonGroup.interactable = !isActive)
                .AddTo(_disposables);
            _confirmButton.OnClickAsObservable()
                .Subscribe(_ => ShowConfirmAsync(destroyCancellationToken).Forget())
                .AddTo(_disposables);
            _rewardButton.OnClickAsObservable()
                .Subscribe(_ => ShowRewardAsync(destroyCancellationToken).Forget())
                .AddTo(_disposables);
            _priorityTestButton.OnClickAsObservable()
                .Subscribe(_ => RunPriorityTest())
                .AddTo(_disposables);
            _legacyButton.OnClickAsObservable()
                .Subscribe(_ => ShowLegacyAsync(destroyCancellationToken).Forget())
                .AddTo(_disposables);
        }

        private async UniTask ShowConfirmAsync(CancellationToken cancellationToken)
        {
            var result = await _popupService.ShowAsync<ConfirmPopup, PopupResult>(
                new PopupParameter
                {
                    Title = "確認",
                    Message = "実行しますか？",
                    ConfirmLabel = "はい",
                    CancelLabel = "いいえ",
                },
                cancellationToken);

            Debug.Log($"[Popup] Confirm 結果: {result}");
        }

        private async UniTask ShowRewardAsync(CancellationToken cancellationToken)
        {
            var result = await _popupService.ShowAsync<RewardPopup, RewardPopupResult>(
                new RewardPopupParameter
                {
                    RewardName = "コイン",
                    Amount = RewardAmount,
                },
                cancellationToken);

            Debug.Log($"[Popup] Reward 結果: claimed={result.Claimed}, amount={result.Amount}");
        }

        private void RunPriorityTest()
        {
            ShowPriorityRewardAsync(PopupPriority.System, destroyCancellationToken).Forget();
            ShowPriorityRewardAsync(PopupPriority.Low, destroyCancellationToken).Forget();
            ShowPriorityRewardAsync(PopupPriority.High, destroyCancellationToken).Forget();
            ShowPriorityRewardAsync(PopupPriority.Normal, destroyCancellationToken).Forget();
        }

        private async UniTask ShowPriorityRewardAsync(
            PopupPriority priority,
            CancellationToken cancellationToken)
        {
            var result = await _popupService.ShowAsync<RewardPopup, RewardPopupResult>(
                new RewardPopupParameter
                {
                    RewardName = priority.ToString(),
                    Amount = PriorityRewardAmount,
                    Priority = priority,
                },
                cancellationToken);

            Debug.Log(
                $"[Popup] Priority 結果: priority={priority}, claimed={result.Claimed}, amount={result.Amount}");
        }

        private async UniTask ShowLegacyAsync(CancellationToken cancellationToken)
        {
            var result = await _legacyManager.ShowAsync(
                new PopupParameter
                {
                    Title = "v1",
                    Message = "旧APIの確認",
                    ConfirmLabel = "OK",
                },
                cancellationToken);

            Debug.Log($"[Popup] Legacy 結果: {result}");
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
            ((IDisposable)_popupService).Dispose();
        }
    }
}
