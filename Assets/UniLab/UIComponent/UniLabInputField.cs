using R3;
using TMPro;
using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// Base MonoBehaviour wrapper for TMP_InputField that exposes an R3 Observable for text changes.
    /// Inherit to add project-specific styling or additional fields.
    /// </summary>
    public class UniLabInputField : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _inputField;

        private readonly Subject<string> _onTextChanged = new();

        /// <summary>Emits the current text value whenever the input field content changes.</summary>
        public Observable<string> OnTextChanged => _onTextChanged;

        /// <summary>Returns the current text value.</summary>
        public string GetText()
        {
            return _inputField.text;
        }

        /// <summary>Sets the text value and triggers OnTextChanged.</summary>
        public void SetText(string text)
        {
            _inputField.text = text;
        }

        /// <summary>Sets the text value without triggering OnTextChanged.</summary>
        public void SetTextWithoutNotify(string text)
        {
            _inputField.SetTextWithoutNotify(text);
        }

        /// <summary>Clears the input field and triggers OnTextChanged.</summary>
        public void Clear()
        {
            _inputField.text = string.Empty;
        }

        protected virtual void Awake()
        {
            _inputField.onValueChanged.AddListener(value => _onTextChanged.OnNext(value));
        }

        protected virtual void OnDestroy()
        {
            _onTextChanged.Dispose();
        }
    }
}
