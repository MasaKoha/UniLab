using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup.Sample
{
    /// <summary>
    /// Popup 基盤 v2 の表示・優先度制御・入力ブロックを確認する起点。
    /// プレハブは Resources からロードするため、Editor でのアセット参照配線に依存しない。
    /// </summary>
    public sealed class PopupSampleEntry : MonoBehaviour
    {
        private const int RewardAmount = 100;
        private const int PriorityRewardAmount = 1;

        [SerializeField] private Transform _popupRoot = null;
        [SerializeField] private Button _confirmButton = null;
        [SerializeField] private Button _rewardButton = null;
        [SerializeField] private Button _priorityTestButton = null;
        [SerializeField] private CanvasGroup _buttonGroup = null;

        private IPopupService _popupService = null;
        private readonly CompositeDisposable _disposables = new();

        private void Awake()
        {
            _popupService = new PopupService(new ResourcesPopupViewProvider(_popupRoot));
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
        }

        private async UniTask ShowConfirmAsync(CancellationToken cancellationToken)
        {
            var result = await _popupService.ShowAsync<ConfirmPopup, PopupResult>(
                new PopupParameter
                {
                    Title = "Confirm",
                    Message = "Are you sure?",
                    ConfirmLabel = "Yes",
                    CancelLabel = "No",
                },
                cancellationToken);

            Debug.Log($"[Popup] Confirm result: {result}");
        }

        private async UniTask ShowRewardAsync(CancellationToken cancellationToken)
        {
            var result = await _popupService.ShowAsync<RewardPopup, RewardPopupResult>(
                new RewardPopupParameter
                {
                    RewardName = "Coin",
                    Amount = RewardAmount,
                },
                cancellationToken);

            Debug.Log($"[Popup] Reward result: claimed={result.Claimed}, amount={result.Amount}");
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

            Debug.Log($"[Popup] Priority result: priority={priority}, claimed={result.Claimed}");
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
            ((IDisposable)_popupService).Dispose();
        }

        /// <summary>
        /// Resources からポップアッププレハブをロードする ViewProvider。
        /// Editor でのプレハブ参照配線を介さず、型名で Resources/Popup/{型名} を解決する。
        /// </summary>
        private sealed class ResourcesPopupViewProvider : IPopupViewProvider
        {
            private readonly Transform _popupRoot;

            public ResourcesPopupViewProvider(Transform popupRoot)
            {
                _popupRoot = popupRoot;
            }

            public UniTask<TPopup> LoadAsync<TPopup>(CancellationToken cancellationToken) where TPopup : PopupBase
            {
                var prefab = Resources.Load<TPopup>($"Popup/{typeof(TPopup).Name}");
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Resources/Popup/{typeof(TPopup).Name} が見つかりません。");
                }

                var instance = UnityEngine.Object.Instantiate(prefab, _popupRoot);
                instance.gameObject.SetActive(false);
                return UniTask.FromResult(instance);
            }

            public void Release(PopupBase popup)
            {
                UnityEngine.Object.Destroy(popup.gameObject);
            }
        }
    }
}
