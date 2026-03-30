using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// Popup view for confirmation/cancellation interactions.
    /// Inherits PopupBase to integrate with the existing popup stack system.
    /// </summary>
    public class ConfirmPopup : PopupBase
    {
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

            _confirmButton.onClick.AddListener(() => _resultSource.TrySetResult(PopupResult.Confirm));
            _cancelButton.onClick.AddListener(() => _resultSource.TrySetResult(PopupResult.Cancel));
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
            await _resultSource.Task;
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
            _resultSource.TrySetResult(PopupResult.Cancel);
        }

        /// <summary>
        /// Returns a UniTask that completes with the user's response.
        /// Must be awaited before WaitPopupAsync destroys the instance.
        /// </summary>
        public UniTask<PopupResult> GetResultAsync()
        {
            return _resultSource.Task;
        }
    }
}
