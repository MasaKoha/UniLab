using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UniLab.UI.Tween;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 確認 / キャンセルの 2 択ポップアップ View。既存のポップアップスタックに乗せるため PopupBase を継承する。
    /// </summary>
    public class ConfirmPopup : PopupBase
    {
        // 開閉アニメーションの尺。DOTween 撤去に伴い UiTween へ渡す定数として保持する。
        private const float OpenDuration = 0.25f;
        private const float CloseDuration = 0.2f;

        [SerializeField] private TMP_Text _titleText = null;
        [SerializeField] private TMP_Text _messageText = null;
        [SerializeField] private Button _confirmButton = null;
        [SerializeField] private Button _cancelButton = null;

        private UniTaskCompletionSource<PopupResult> _resultSource;

        protected override void OnInitialize()
        {
            _resultSource = new UniTaskCompletionSource<PopupResult>();

            var parameter = (PopupParameter)Parameter;

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
                .Subscribe(_ => _resultSource.TrySetResult(PopupResult.Confirm))
                .AddTo(this);
            _cancelButton.OnClickAsObservable()
                .Subscribe(_ => _resultSource.TrySetResult(PopupResult.Cancel))
                .AddTo(this);
        }

        /// <summary>
        /// 0 倍から等倍へ、勢いをつけて開くアニメーションを再生する。
        /// </summary>
        public override async UniTask OpenAsync()
        {
            // destroyCancellationToken で外部破棄・シーン遷移時にアニメーションを安全に中断する
            await UiTween.ScaleAsync(
                transform, Vector3.zero, Vector3.one, OpenDuration, EaseType.OutBack, destroyCancellationToken);
        }

        /// <summary>
        /// ユーザーが確認 / キャンセルを押すまで待機し、その後に閉じるアニメーションへ繋ぐ。
        /// </summary>
        public override async UniTask WaitAsync()
        {
            await _resultSource.Task;
            await CloseAsync();
        }

        /// <summary>
        /// 等倍から 0 倍へ縮小して閉じるアニメーションを再生する。
        /// </summary>
        public override async UniTask CloseAsync()
        {
            await UiTween.ScaleAsync(
                transform, transform.localScale, Vector3.zero, CloseDuration, EaseType.InBack, destroyCancellationToken);
        }

        /// <summary>
        /// バックキー / 背景タップで閉じられた場合に呼ばれ、結果を Cancel として解決する。
        /// </summary>
        public override void OnClose()
        {
            _resultSource.TrySetResult(PopupResult.Cancel);
        }

        /// <summary>
        /// ユーザー操作の結果で完了する UniTask を返す。Initialize 後に呼ぶこと（事前条件）。
        /// </summary>
        public UniTask<PopupResult> GetResultAsync()
        {
            return _resultSource.Task;
        }
    }
}
