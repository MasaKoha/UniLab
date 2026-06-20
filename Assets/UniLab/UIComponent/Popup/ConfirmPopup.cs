using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 確認 / キャンセルの 2 択ポップアップ View。結果型は PopupResult。開閉アニメは Transition に委譲する。
    /// </summary>
    public sealed class ConfirmPopup : PopupBase<PopupParameter, PopupResult>
    {
        [SerializeField] private TMP_Text _titleText = null;
        [SerializeField] private TMP_Text _messageText = null;
        [SerializeField] private Button _confirmButton = null;
        [SerializeField] private Button _cancelButton = null;

        protected override void OnSetup(PopupParameter parameter)
        {
            _titleText.text = parameter.Title;
            _messageText.text = parameter.Message;
            _confirmButton.GetComponentInChildren<TMP_Text>().text = parameter.ConfirmLabel;

            var hasCancelButton = parameter.CancelLabel != null;
            _cancelButton.gameObject.SetActive(hasCancelButton);
            if (hasCancelButton)
            {
                _cancelButton.GetComponentInChildren<TMP_Text>().text = parameter.CancelLabel;
            }

            // AddTo(this) で破棄時に購読解除し、プール化・再利用時のリスナー多重登録を防ぐ
            _confirmButton.OnClickAsObservable()
                .Subscribe(_ => SetResult(PopupResult.Confirm))
                .AddTo(this);
            _cancelButton.OnClickAsObservable()
                .Subscribe(_ => SetResult(PopupResult.Cancel))
                .AddTo(this);
        }

        /// <summary>
        /// バックキー / 背景タップで閉じられた場合に呼ばれ、結果を Cancel として解決する。
        /// </summary>
        public override void OnClose()
        {
            SetResult(PopupResult.Cancel);
        }
    }
}
