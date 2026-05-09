using System.Collections.Generic;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.Tools.Editor.MissingChecker
{
    [InitializeOnLoad]
    public static class HierarchyMissingChecker
    {
#if UNITY_6000_4_OR_NEWER
        private static readonly HashSet<EntityId> _missingIds = new();
        private static readonly HashSet<EntityId> _missingSelfIds = new();
#else
        private static readonly HashSet<int> _missingIds = new();
        private static readonly HashSet<int> _missingSelfIds = new();
#endif
        private static bool _needsRebuild = true;
        private static double _lastRebuildTime = -1;
        private const double RebuildCooldownSeconds = 0.5;

        static HierarchyMissingChecker()
        {
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyItemGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;
#endif
            EditorApplication.hierarchyChanged += MarkDirty;
            EditorApplication.projectChanged += MarkDirty;
            EditorApplication.playModeStateChanged += _ => MarkDirty();
            EditorSceneManager.sceneOpened += (_, _) => MarkDirty();
            EditorSceneManager.sceneClosed += _ => MarkDirty();
            PrefabStage.prefabStageOpened += _ => MarkDirty();
            PrefabStage.prefabStageClosing += _ => MarkDirty();
        }

        // Why: OnHierarchyItemGUI 側にも isPlaying ガードがあるが、
        // Play 中に無駄なダーティフラグが蓄積されるのを防ぐためここでも弾く。
        private static void MarkDirty()
        {
            if (Application.isPlaying)
            {
                return;
            }

            _needsRebuild = true;
        }

#if UNITY_6000_4_OR_NEWER
        private static void OnHierarchyItemGUI(EntityId entityId, Rect selectionRect)
#else
        private static void OnHierarchyItemGUI(int entityId, Rect selectionRect)
#endif
        {
            // Why: RebuildCache は全 GameObject を SerializedObject で走査するため、Play 中は絶対に走らせない。
            if (Application.isPlaying)
            {
                return;
            }

            if (_needsRebuild)
            {
                RebuildCache();
            }

            if (!_missingIds.Contains(entityId))
            {
                return;
            }

            if (Settings == null || !Settings.EnableHierarchyHighlight)
            {
                return;
            }

            var labelRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.width, selectionRect.height);
            var bgRect = new Rect(labelRect.x, labelRect.y + 1f, labelRect.width, labelRect.height - 2f);
            var isSelfMissing = _missingSelfIds.Contains(entityId);
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
            // perf: Edit モードでの連続 Hierarchy 変更（Undo/Redo、Prefab 展開等）による
            // 全シーン再スキャンスパイクを抑制するデバウンス。
            // Play 中はここに到達しない（OnHierarchyItemGUI と MarkDirty の両方でガード済み）。
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastRebuildTime < RebuildCooldownSeconds)
            {
                return;
            }

            _lastRebuildTime = now;
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
            var hasMissingInSelf = MissingReferenceUtility.HasMissingReferences(go);
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
#if UNITY_6000_4_OR_NEWER
                    _missingSelfIds.Add(go.GetEntityId());
#else
                    _missingSelfIds.Add(go.GetInstanceID());
#endif
                }

#if UNITY_6000_4_OR_NEWER
                _missingIds.Add(go.GetEntityId());
#else
                _missingIds.Add(go.GetInstanceID());
#endif
                return true;
            }

            return false;
        }

    }
}
