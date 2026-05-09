using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.ProjectScanCommon
{
    /// <summary>
    /// Shared editor utility methods used across project scan tools (missing checker, reference finder, etc.).
    /// </summary>
    public static class ProjectScanEditorUtility
    {
        // perf: cache reflection lookup to avoid repeated GetMethod calls
        private static readonly MethodInfo RepaintProjectWindowMethod =
            typeof(EditorApplication).GetMethod("RepaintProjectWindow",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Repaints the Project window via reflection.
        /// </summary>
        public static void RepaintProjectWindow()
        {
            RepaintProjectWindowMethod?.Invoke(null, null);
        }

        /// <summary>
        /// Recursively creates AssetDatabase folders so that the full path exists.
        /// </summary>
        public static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var name = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent))
            {
                parent = "Assets";
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>
        /// Recursively collects GUIDs of all parent folders for the given asset path.
        /// </summary>
        public static void CollectParentFolderGuids(string assetPath, HashSet<string> parentGuids)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            var folder = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            folder = folder.Replace('\\', '/');
            while (!string.IsNullOrEmpty(folder) && folder != "Assets")
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    var guid = AssetDatabase.AssetPathToGUID(folder);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        parentGuids.Add(guid);
                    }
                }

                var next = Path.GetDirectoryName(folder);
                if (string.IsNullOrEmpty(next))
                {
                    break;
                }

                folder = next.Replace('\\', '/');
            }
        }

        /// <summary>
        /// Finds the first ScriptableObject asset of the specified type in the project.
        /// </summary>
        public static T FindSettingsAsset<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// <summary>
        /// Creates a ScriptableObject asset at the specified path, ensuring parent folders exist.
        /// </summary>
        public static T CreateSettingsAsset<T>(string assetPath) where T : ScriptableObject
        {
            var folderPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = "Assets/Generated/UniCore";
            }

            EnsureFolderExists(folderPath);
            var settings = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>
        /// Adds non-empty GUIDs from the source collection into the target HashSet.
        /// </summary>
        public static void FillGuidSet(HashSet<string> set, IEnumerable<string> guids)
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

        /// <summary>
        /// Escapes a string value for safe inclusion in a CSV field.
        /// Wraps the value in double-quotes and escapes inner double-quotes when necessary.
        /// </summary>
        public static string EscapeCsv(string value)
        {
            if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n") && !value.Contains("\r"))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Draws a standard asset row with a file-name button (select + ping) and an "開く" button.
        /// Asset loading is deferred to button click to avoid per-frame AssetDatabase access in OnGUI.
        /// </summary>
        public static void DrawAssetRow(string assetPath)
        {
            var fileName = Path.GetFileName(assetPath);
            if (GUILayout.Button(fileName, EditorStyles.miniButtonLeft))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }

            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.Open), EditorStyles.miniButtonRight, GUILayout.Width(50)))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset);
                }
            }
        }

        /// <summary>
        /// Builds a "/" separated Hierarchy path from root to the specified Transform.
        /// </summary>
        public static string BuildGameObjectPath(Transform target)
        {
            var parts = new System.Collections.Generic.List<string>();
            var current = target;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
