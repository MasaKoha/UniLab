using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup.Sample
{
    /// <summary>
    /// 任意の結果型を確認する報酬ポップアップ。開閉アニメは Transition（フェード）に委譲する。
    /// </summary>
    public sealed class RewardPopup : PopupBase<RewardPopupParameter, RewardPopupResult>
    {
        [SerializeField] private TMP_Text _titleText = null;
        [SerializeField] private TMP_Text _rewardText = null;
        [SerializeField] private Button _claimButton = null;
        [SerializeField] private Button _closeButton = null;

        protected override void OnSetup(RewardPopupParameter parameter)
        {
            _titleText.text = "Reward!";
            _rewardText.text = $"{parameter.RewardName} x{parameter.Amount}";

            // Reward ポップアップ表示時のログ
            Debug.Log($"[Popup] Reward shown: priority={parameter.Priority}");

            _claimButton.OnClickAsObservable()
                .Subscribe(_ => SetResult(new RewardPopupResult(true, parameter.Amount)))
                .AddTo(this);
            _closeButton.OnClickAsObservable()
                .Subscribe(_ => SetResult(new RewardPopupResult(false, 0)))
                .AddTo(this);
        }

        /// <summary>背景タップまたはバックキーによる終了を未受領として解決する。</summary>
        public override void OnClose()
        {
            SetResult(new RewardPopupResult(false, 0));
        }
    }
}
