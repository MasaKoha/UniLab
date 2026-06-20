using UniLab.Tools.Editor.ProjectScanCommon;
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

            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.TargetFolders), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(EditorToolLabels.Get(LabelKey.TargetFoldersHint), MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetFolders"), true);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.ExtensionsCsvLabel), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_extensionsCsv"), GUIContent.none);
            EditorGUILayout.Space(6);

            // Project 背景色
            EditorGUILayout.LabelField("Project Background Color", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_projectSelfBackgroundColor"), new GUIContent("Missing Itself"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_projectParentBackgroundColor"), new GUIContent("Parent Folder"));
            EditorGUILayout.Space(6);

            // ヒエラルキー
            EditorGUILayout.LabelField("Hierarchy", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_enableHierarchyHighlight"), new GUIContent("Enable Highlight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hierarchySelfBackgroundColor"), new GUIContent("Missing Itself"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hierarchyParentBackgroundColor"), new GUIContent("Parent"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hierarchyIconColor"), new GUIContent("Icon Color"));

            if (serializedObject.ApplyModifiedProperties())
            {
                settings.SaveAsset();
            }
        }
    }
}