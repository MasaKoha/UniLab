using UniLab.Tools.Editor.ProjectScanCommon;
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

            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.TargetFolders), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(EditorToolLabels.Get(LabelKey.TargetFoldersHint), MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetFolders"), true);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.ExtensionsCsvLabel), EditorStyles.boldLabel);
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
            var nextColor = EditorGUILayout.ColorField(EditorToolLabels.Get(LabelKey.ReferenceColorLabel), settings.ProjectReferenceBackgroundColor);
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