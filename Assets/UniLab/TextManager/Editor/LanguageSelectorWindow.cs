#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UniLab.TextManager.Editor
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
            _selectedLanguage = "ja"; // デフォルト
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Select Language", EditorStyles.boldLabel);

            // 文字列入力欄のみ
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