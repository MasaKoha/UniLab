using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UniLab.UI.Tween;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup.Sample
{
    /// <summary>
    /// 任意の結果型とフェードアニメーションを確認する報酬ポップアップ。
    /// </summary>
    public sealed class RewardPopup : PopupBase<RewardPopupParameter, RewardPopupResult>
    {
        private const float OpenDuration = 0.25f;
        private const float CloseDuration = 0.2f;

        [SerializeField] private TMP_Text _titleText = null;
        [SerializeField] private TMP_Text _rewardText = null;
        [SerializeField] private Button _claimButton = null;
        [SerializeField] private Button _closeButton = null;
        [SerializeField] private CanvasGroup _canvasGroup = null;

        protected override void OnSetup(RewardPopupParameter parameter)
        {
            _titleText.text = "Reward!";
            _rewardText.text = $"{parameter.RewardName} x{parameter.Amount}";

            Debug.Log($"[Popup] Reward 表示: priority={parameter.Priority}");

            _claimButton.OnClickAsObservable()
                .Subscribe(_ => SetResult(new RewardPopupResult(true, parameter.Amount)))
                .AddTo(this);
            _closeButton.OnClickAsObservable()
                .Subscribe(_ => SetResult(new RewardPopupResult(false, 0)))
                .AddTo(this);
        }

        /// <summary>透明状態からフェードインして表示する。</summary>
        public override async UniTask OpenAsync()
        {
            await UiTween.FadeAsync(
                _canvasGroup,
                0f,
                1f,
                OpenDuration,
                EaseType.OutQuad,
                destroyCancellationToken);
        }

        /// <summary>現在の透明度からフェードアウトして閉じる。</summary>
        public override async UniTask CloseAsync()
        {
            await UiTween.FadeAsync(
                _canvasGroup,
                _canvasGroup.alpha,
                0f,
                CloseDuration,
                EaseType.InQuad,
                destroyCancellationToken);
        }

        /// <summary>背景タップまたはバックキーによる終了を未受領として解決する。</summary>
        public override void OnClose()
        {
            SetResult(new RewardPopupResult(false, 0));
        }
    }
}
