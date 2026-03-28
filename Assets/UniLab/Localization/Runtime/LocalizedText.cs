using TMPro;
using UniLab.Localization.Editor;
using UnityEngine;

namespace UniLab.Localization
{
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField, LocalizationKeyDropdown]
        private string _key;

        private uint KeyHash => Localization.KeyHash.Fnv1AHash(_key);
        private TextMeshProUGUI _text;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            TextManager.OnLanguageChanged += UpdateText;
            UpdateText();
        }

        private void OnDisable()
        {
            TextManager.OnLanguageChanged -= UpdateText;
        }

        private void OnValidate()
        {
            UpdateText();
        }

        private void UpdateText()
        {
            if (_text == null)
            {
                _text = GetComponent<TextMeshProUGUI>();
            }

            _text.text = string.IsNullOrEmpty(_key) ? "" : TextManager.GetByHash(KeyHash);
        }
    }
}
