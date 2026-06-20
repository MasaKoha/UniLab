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
        [SerializeField] private PopupDimmer _dimmer = null;
        [SerializeField] private Button _confirmButton = null;
        [SerializeField] private Button _rewardButton = null;
        [SerializeField] private Button _priorityTestButton = null;
        [SerializeField] private Button _sequenceButton = null;
        [SerializeField] private Button _stackButton = null;
        [SerializeField] private CanvasGroup _buttonGroup = null;

        private IPopupService _popupService = null;
        private PopupBackKeyHandler _backKeyHandler = null;
        private readonly CompositeDisposable _disposables = new();

        private void Awake()
        {
            // ロード手段は IPopupAssetLoader で差し替え可能。サンプルは Resources 版を使う（本番は AssetVault 版に差し替え）。
            // 共通暗幕を注入し、各ポップアップは個別背景を持たず 1 枚を最前面ポップアップの背後に共有する
            var viewProvider = new PopupViewProvider(new ResourcesPopupAssetLoader(), _popupRoot);
            _popupService = new PopupService(viewProvider, _dimmer);
            // 非 DI 環境のため手動で生成・購読開始する。DI 環境では PopupInstaller が代行する
            _backKeyHandler = new PopupBackKeyHandler(_popupService);
            _backKeyHandler.Start();
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
            _sequenceButton.OnClickAsObservable()
                .Subscribe(_ => ShowSequenceAsync(destroyCancellationToken).Forget())
                .AddTo(_disposables);
            _stackButton.OnClickAsObservable()
                .Subscribe(_ => ShowStackAsync(destroyCancellationToken).Forget())
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

        /// <summary>
        /// 複数ポップアップを逐次表示するサンプル。1 枚目の確認で「はい」を選んだ場合のみ 2 枚目の報酬を続けて出す。
        /// 単一表示モデルでも await で繋ぐだけでチェーン表示でき、前段の結果で後段を分岐できることを示す。
        /// </summary>
        private async UniTask ShowSequenceAsync(CancellationToken cancellationToken)
        {
            var confirmResult = await _popupService.ShowAsync<ConfirmPopup, PopupResult>(
                new PopupParameter
                {
                    Title = "Sequence",
                    Message = "Open the reward next?",
                    ConfirmLabel = "Open",
                    CancelLabel = "No",
                },
                cancellationToken);

            // キャンセルされたら後段は出さずに打ち切る
            if (confirmResult != PopupResult.Confirm)
            {
                Debug.Log("[Popup] Sequence: canceled at confirm");
                return;
            }

            var rewardResult = await _popupService.ShowAsync<RewardPopup, RewardPopupResult>(
                new RewardPopupParameter
                {
                    RewardName = "Gem",
                    Amount = RewardAmount,
                },
                cancellationToken);

            Debug.Log($"[Popup] Sequence done: claimed={rewardResult.Claimed}, amount={rewardResult.Amount}");
        }

        /// <summary>
        /// オプトイン・スタックのサンプル。下にベース（Reward）を出し、その上に Stack=true の確認を重ねて 2 枚同時表示する。
        /// 上を閉じるとベースが残り、暗幕がベースの背後へ移動して再び操作可能になることを確認できる。
        /// </summary>
        private async UniTask ShowStackAsync(CancellationToken cancellationToken)
        {
            // ベースは直列キュー経由（Stack=false）。閉じるまで完了しないため Forget して走らせ続ける
            var baseTask = _popupService.ShowAsync<RewardPopup, RewardPopupResult>(
                new RewardPopupParameter
                {
                    RewardName = "Base",
                    Amount = RewardAmount,
                },
                cancellationToken);

            // ベースが生成・表示されてから重ねるため 1 フレーム待つ
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            var stackedResult = await _popupService.ShowAsync<ConfirmPopup, PopupResult>(
                new PopupParameter
                {
                    Title = "Stacked",
                    Message = "On top of the base popup.",
                    ConfirmLabel = "Close",
                    Stack = true,
                },
                cancellationToken);

            Debug.Log($"[Popup] Stacked closed: {stackedResult}. Base still open underneath.");
            baseTask.Forget();
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
            _backKeyHandler.Dispose();
            ((IDisposable)_popupService).Dispose();
        }
    }
}
