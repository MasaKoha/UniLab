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
        /// <summary>
        /// Repaints the Project window via reflection.
        /// </summary>
        public static void RepaintProjectWindow()
        {
            var method = typeof(EditorApplication).GetMethod("RepaintProjectWindow",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            method?.Invoke(null, null);
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
    }
}
