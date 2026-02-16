using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.AssetReferenceFinder
{
    [CustomEditor(typeof(AssetReferenceFinderSettings))]
    public class AssetReferenceFinderSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var settings = (AssetReferenceFinderSettings)target;

            EditorGUILayout.LabelField("スキャン対象フォルダ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("空欄の場合は全フォルダを対象にスキャンします。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetFolders"), true);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("拡張子 (CSV, ドット不要)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var nextExtensionsCsv = EditorGUILayout.TextField(GUIContent.none, settings.ExtensionsCsv);
            if (EditorGUI.EndChangeCheck())
            {
                settings.ExtensionsCsv = nextExtensionsCsv;
                settings.SaveAsset();
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Project 背景色", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var nextColor = EditorGUILayout.ColorField("参照アセット", settings.ProjectReferenceBackgroundColor);
            if (EditorGUI.EndChangeCheck())
            {
                settings.ProjectReferenceBackgroundColor = nextColor;
                settings.SaveAsset();
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                settings.SaveAsset();
            }
        }
    }
}