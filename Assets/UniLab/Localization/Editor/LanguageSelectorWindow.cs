#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UniLab.Localization.Editor
{
    public class LanguageSelectorWindow : EditorWindow
    {
        private string _selectedLanguage = "ja";

        [MenuItem("UniLab/TextManager/Select Language")]
        public static void ShowWindow()
        {
            GetWindow<LanguageSelectorWindow>("Language Selector");
        }

        private void OnEnable()
        {
            _selectedLanguage = "ja"; // Default
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Select Language", EditorStyles.boldLabel);

            _selectedLanguage = EditorGUILayout.TextField("Language (string)", _selectedLanguage);

            if (GUILayout.Button("Apply"))
            {
                TextManager.SetLanguage(_selectedLanguage);
                Debug.Log("Language set to: " + _selectedLanguage);
            }
        }
    }
}
#endif
