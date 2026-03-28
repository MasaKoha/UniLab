using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using TMPro;
using UniLab.Popup;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.Feature.UI.Dialog
{
    /// <summary>
    /// Popup view for dialog confirmation/cancellation interactions.
    /// Inherits PopupBase to integrate with the existing popup stack system.
    /// </summary>
    public class DialogPopup : PopupBase
    {
        [SerializeField] private TMP_Text _titleText = null;
        [SerializeField] private TMP_Text _messageText = null;
        [SerializeField] private Button _confirmButton = null;
        [SerializeField] private Button _cancelButton = null;

        private readonly Subject<DialogResult> _resultSubject = new();

        protected override void OnInitialize()
        {
            var dialogParameter = (DialogParameter)Parameter;

            _titleText.text = dialogParameter.Title;
            _messageText.text = dialogParameter.Message;
            _confirmButton.GetComponentInChildren<TMP_Text>().text = dialogParameter.ConfirmLabel;

            var hasCancelButton = dialogParameter.CancelLabel != null;
            _cancelButton.gameObject.SetActive(hasCancelButton);
            if (hasCancelButton)
            {
                _cancelButton.GetComponentInChildren<TMP_Text>().text = dialogParameter.CancelLabel;
            }

            _confirmButton.OnClickAsObservable()
                .Subscribe(_ => _resultSubject.OnNext(DialogResult.Confirm))
                .AddTo(this);

            _cancelButton.OnClickAsObservable()
                .Subscribe(_ => _resultSubject.OnNext(DialogResult.Cancel))
                .AddTo(this);
        }

        /// <summary>
        /// Scales the popup from 0 to 1 with an ease-out animation.
        /// </summary>
        public override async UniTask OpenAsync()
        {
            transform.localScale = Vector3.zero;
            await transform
                .DOScale(Vector3.one, 0.25f)
                .SetEase(Ease.OutBack)
                .ToUniTask();
        }

        /// <summary>
        /// Waits until the user taps confirm or cancel, then triggers CloseAsync.
        /// </summary>
        public override async UniTask WaitAsync()
        {
            await _resultSubject.FirstAsync();
            await CloseAsync();
        }

        /// <summary>
        /// Scales the popup from 1 to 0 with an ease-in animation.
        /// </summary>
        public override async UniTask CloseAsync()
        {
            await transform
                .DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .ToUniTask();
        }

        /// <summary>
        /// Called when the back key is triggered; resolves as Cancel.
        /// </summary>
        public override void OnClose()
        {
            _resultSubject.OnNext(DialogResult.Cancel);
        }

        /// <summary>
        /// Returns a UniTask that completes with the user's response.
        /// Must be awaited before WaitPopupAsync destroys the instance.
        /// </summary>
        public UniTask<DialogResult> GetResultAsync()
        {
            return _resultSubject.FirstAsync().AsUniTask();
        }
    }
}
