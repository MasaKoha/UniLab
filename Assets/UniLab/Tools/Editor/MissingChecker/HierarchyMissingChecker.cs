using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.Tools.Editor.MissingChecker
{
    [InitializeOnLoad]
    public static class HierarchyMissingChecker
    {
        private static readonly HashSet<int> _missingIds = new();
        private static readonly HashSet<int> _missingSelfIds = new();
        private static bool _needsRebuild = true;

        static HierarchyMissingChecker()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;
            EditorApplication.hierarchyChanged += MarkDirty;
            EditorApplication.projectChanged += MarkDirty;
            EditorApplication.playModeStateChanged += _ => MarkDirty();
            EditorSceneManager.sceneOpened += (_, _) => MarkDirty();
            EditorSceneManager.sceneClosed += _ => MarkDirty();
            PrefabStage.prefabStageOpened += _ => MarkDirty();
            PrefabStage.prefabStageClosing += _ => MarkDirty();
        }

        private static void MarkDirty()
        {
            _needsRebuild = true;
        }

        private static void OnHierarchyItemGUI(int instanceId, Rect selectionRect)
        {
            if (_needsRebuild)
            {
                RebuildCache();
            }

            if (!_missingIds.Contains(instanceId))
            {
                return;
            }

            if (Settings == null || !Settings.EnableHierarchyHighlight)
            {
                return;
            }

            var labelRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.width, selectionRect.height);
            var bgRect = new Rect(labelRect.x, labelRect.y + 1f, labelRect.width, labelRect.height - 2f);
            var isSelfMissing = _missingSelfIds.Contains(instanceId);
            EditorGUI.DrawRect(bgRect, isSelfMissing ? Settings.HierarchySelfBackgroundColor : Settings.HierarchyParentBackgroundColor);

            if (isSelfMissing)
            {
                var iconRect = new Rect(selectionRect.xMax - 18f, selectionRect.y, 18f, selectionRect.height);
                var prevColor = GUI.color;
                GUI.color = Settings.HierarchyIconColor;
                EditorGUI.LabelField(iconRect, "⚠");
                GUI.color = prevColor;
            }
        }

        private static ProjectMissingCheckerSettings Settings => ProjectMissingCheckerSettings.GetOrCreate();

        private static void RebuildCache()
        {
            _missingIds.Clear();
            _missingSelfIds.Clear();

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                foreach (var root in prefabStage.scene.GetRootGameObjects())
                {
                    CollectMissing(root);
                }

                _needsRebuild = false;
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    CollectMissing(root);
                }
            }

            _needsRebuild = false;
        }

        private static bool CollectMissing(GameObject go)
        {
            var hasMissingInSelf = HasMissingReferences(go);
            var hasMissingInChildren = false;

            var transform = go.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (CollectMissing(transform.GetChild(i).gameObject))
                {
                    hasMissingInChildren = true;
                }
            }

            if (hasMissingInSelf || hasMissingInChildren)
            {
                if (hasMissingInSelf)
                {
                    _missingSelfIds.Add(go.GetInstanceID());
                }

                _missingIds.Add(go.GetInstanceID());
                return true;
            }

            return false;
        }

        private static bool HasMissingReferences(GameObject go)
        {
            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    return true;
                }

                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}