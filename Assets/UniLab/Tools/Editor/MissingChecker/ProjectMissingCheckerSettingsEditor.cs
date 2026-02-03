using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.MissingChecker
{
    [CustomEditor(typeof(ProjectMissingCheckerSettings))]
    public class ProjectMissingCheckerSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var settings = (ProjectMissingCheckerSettings)target;

            EditorGUILayout.LabelField("スキャン対象フォルダ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("空欄の場合は全フォルダを対象にスキャンします。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetFolders"), true);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("拡張子 (CSV, ドット不要)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_extensionsCsv"), GUIContent.none);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Project 背景色", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_projectSelfBackgroundColor"), new GUIContent("Missing 自身"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_projectParentBackgroundColor"), new GUIContent("親フォルダ"));
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("ヒエラルキー", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_enableHierarchyHighlight"), new GUIContent("色付けを有効"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hierarchySelfBackgroundColor"), new GUIContent("Missing 自身"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hierarchyParentBackgroundColor"), new GUIContent("親"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hierarchyIconColor"), new GUIContent("アイコン色"));

            if (serializedObject.ApplyModifiedProperties())
            {
                settings.SaveAsset();
            }
        }
    }
}