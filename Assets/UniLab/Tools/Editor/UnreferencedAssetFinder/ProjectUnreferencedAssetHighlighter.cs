using System.Collections.Generic;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.UnreferencedAssetFinder
{
    /// <summary>
    /// Highlights unreferenced assets and their parent folders in the Project window.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectUnreferencedAssetHighlighter
    {
        private static readonly HashSet<string> _unreferencedSelfGuids = new();
        private static readonly HashSet<string> _unreferencedParentGuids = new();

        static ProjectUnreferencedAssetHighlighter()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectItemGUI;
        }

        /// <summary>
        /// Sets the GUIDs that should be highlighted as unreferenced assets and parent folders.
        /// </summary>
        public static void SetUnreferencedGuids(IEnumerable<string> selfGuids, IEnumerable<string> parentGuids)
        {
            _unreferencedSelfGuids.Clear();
            _unreferencedParentGuids.Clear();
            ProjectScanEditorUtility.FillGuidSet(_unreferencedSelfGuids, selfGuids);
            ProjectScanEditorUtility.FillGuidSet(_unreferencedParentGuids, parentGuids);
            ProjectScanEditorUtility.RepaintProjectWindow();
        }

        /// <summary>
        /// Clears all project window highlights.
        /// </summary>
        public static void Clear()
        {
            if (_unreferencedSelfGuids.Count == 0 && _unreferencedParentGuids.Count == 0)
            {
                return;
            }

            _unreferencedSelfGuids.Clear();
            _unreferencedParentGuids.Clear();
            ProjectScanEditorUtility.RepaintProjectWindow();
        }

        private static void OnProjectItemGUI(string guid, Rect selectionRect)
        {
            if ((_unreferencedSelfGuids.Count == 0 && _unreferencedParentGuids.Count == 0) || string.IsNullOrEmpty(guid))
            {
                return;
            }

            var isSelf = _unreferencedSelfGuids.Contains(guid);
            var isParent = !isSelf && _unreferencedParentGuids.Contains(guid);
            if (!isSelf && !isParent)
            {
                return;
            }

            var settings = UnreferencedAssetFinderSettings.GetOrCreate();
            var backgroundColor = isSelf ? settings.ProjectSelfBackgroundColor : settings.ProjectParentBackgroundColor;
            var backgroundRect = new Rect(selectionRect.x, selectionRect.y + 1f, selectionRect.width, selectionRect.height - 2f);
            EditorGUI.DrawRect(backgroundRect, backgroundColor);

            if (isSelf)
            {
                var iconRect = new Rect(selectionRect.xMax - 18f, selectionRect.y, 18f, selectionRect.height);
                var previousColor = GUI.color;
                GUI.color = Color.red;
                EditorGUI.LabelField(iconRect, "U");
                GUI.color = previousColor;
            }
        }
    }
}
