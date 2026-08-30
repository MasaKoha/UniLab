using R3;
using TMPro;
using UniLab.UI.Focus;
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
        [SerializeField] private TMP_Text _confirmLabel = null;
        [SerializeField] private Button _cancelButton = null;
        [SerializeField] private TMP_Text _cancelLabel = null;

        /// <summary>キャンセルボタンを表示しているか。フォーカスグリッドの組み立てで参照する。</summary>
        private bool _hasCancelButton;

        protected override void OnSetup(PopupParameter parameter)
        {
            _titleText.text = parameter.Title;
            _messageText.text = parameter.Message;
            // ラベルは SerializeField で結線する。GetComponentInChildren は階層変更で静かに壊れるうえ、
            // 毎回の型探索コストも乗るため使わない
            _confirmLabel.text = parameter.ConfirmLabel;

            _hasCancelButton = parameter.CancelLabel != null;
            _cancelButton.gameObject.SetActive(_hasCancelButton);
            if (_hasCancelButton)
            {
                _cancelLabel.text = parameter.CancelLabel;
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
        /// 表示中のボタンだけを 1 行に並べたグリッドを返す。キャンセルが無い場合は確認 1 つだけの行になる。
        /// 見た目の並び（キャンセル左・確認右）に合わせる。
        /// </summary>
        public override FocusGrid BuildFocusGrid()
        {
            var builder = new FocusGridBuilder();
            if (_hasCancelButton)
            {
                builder.AddRow(_cancelButton, _confirmButton);
            }
            else
            {
                builder.AddRow(_confirmButton);
            }

            return builder.Build();
        }

        /// <summary>初期フォーカスは肯定側に置く。誤操作で否定を選びにくくするため。</summary>
        public override Selectable InitialFocus => _confirmButton;

        /// <summary>
        /// バックキー / 背景タップで閉じられた場合に呼ばれ、結果を Cancel として解決する。
        /// </summary>
        public override void OnClose()
        {
            SetResult(PopupResult.Cancel);
        }
    }
}
