using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.AssetReferenceFinder
{
    [InitializeOnLoad]
    public static class ProjectAssetReferenceHighlighter
    {
        private static readonly HashSet<string> _referenceGuids = new();

        static ProjectAssetReferenceHighlighter()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectItemGUI;
        }

        public static void SetReferenceGuids(IEnumerable<string> guids)
        {
            _referenceGuids.Clear();
            FillSet(_referenceGuids, guids);
            RepaintProjectWindow();
        }

        public static void Clear()
        {
            if (_referenceGuids.Count == 0)
            {
                return;
            }

            _referenceGuids.Clear();
            RepaintProjectWindow();
        }

        private static void OnProjectItemGUI(string guid, Rect selectionRect)
        {
            if (_referenceGuids.Count == 0 || string.IsNullOrEmpty(guid))
            {
                return;
            }

            if (!_referenceGuids.Contains(guid))
            {
                return;
            }

            var settings = AssetReferenceFinderSettings.GetOrCreate();
            if (settings == null)
            {
                return;
            }

            var bgRect = new Rect(selectionRect.x, selectionRect.y + 1f, selectionRect.width, selectionRect.height - 2f);
            EditorGUI.DrawRect(bgRect, settings.ProjectReferenceBackgroundColor);

            var iconRect = new Rect(selectionRect.xMax - 18f, selectionRect.y, 18f, selectionRect.height);
            var prevColor = GUI.color;
            GUI.color = Color.yellow;
            EditorGUI.LabelField(iconRect, "R");
            GUI.color = prevColor;
        }

        private static void FillSet(HashSet<string> set, IEnumerable<string> guids)
        {
            if (guids == null)
            {
                return;
            }

            foreach (var guid in guids)
            {
                if (!string.IsNullOrEmpty(guid))
                {
                    set.Add(guid);
                }
            }
        }

        private static void RepaintProjectWindow()
        {
            var method = typeof(EditorApplication).GetMethod("RepaintProjectWindow",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }
    }
}
