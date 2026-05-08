using System.Collections.Generic;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.MissingChecker
{
    [InitializeOnLoad]
    public static class ProjectMissingCheckerProjectHighlighter
    {
        private static readonly HashSet<string> _missingSelfGuids = new();
        private static readonly HashSet<string> _missingParentGuids = new();

        static ProjectMissingCheckerProjectHighlighter()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectItemGUI;
        }

        public static void SetMissingGuids(IEnumerable<string> selfGuids, IEnumerable<string> parentGuids)
        {
            _missingSelfGuids.Clear();
            _missingParentGuids.Clear();
            ProjectScanEditorUtility.FillGuidSet(_missingSelfGuids, selfGuids);
            ProjectScanEditorUtility.FillGuidSet(_missingParentGuids, parentGuids);
        }

        private static void OnProjectItemGUI(string guid, Rect selectionRect)
        {
            if ((_missingSelfGuids.Count == 0 && _missingParentGuids.Count == 0) || string.IsNullOrEmpty(guid))
            {
                return;
            }

            var isSelf = _missingSelfGuids.Contains(guid);
            var isParent = !isSelf && _missingParentGuids.Contains(guid);
            if (!isSelf && !isParent)
            {
                return;
            }

            var settings = ProjectMissingCheckerSettings.GetOrCreate();
            if (settings == null)
            {
                return;
            }

            var bgRect = new Rect(selectionRect.x, selectionRect.y + 1f, selectionRect.width, selectionRect.height - 2f);
            EditorGUI.DrawRect(bgRect, isSelf ? settings.ProjectSelfBackgroundColor : settings.ProjectParentBackgroundColor);

            if (isSelf)
            {
                var iconRect = new Rect(selectionRect.xMax - 18f, selectionRect.y, 18f, selectionRect.height);
                var prevColor = GUI.color;
                GUI.color = Color.yellow;
                EditorGUI.LabelField(iconRect, "⚠");
                GUI.color = prevColor;
            }
        }
    }
}
