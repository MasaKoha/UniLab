using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UniLab.Tools.Editor.HierarchyFavorite
{
    /// <summary>
    /// Serializable entry representing a favorited GameObject in the Hierarchy.
    /// </summary>
    [Serializable]
    public class FavoriteEntry
    {
        /// <summary>
        /// GlobalObjectId string representation for resolving the object across sessions.
        /// </summary>
        public string GlobalObjectIdString;

        /// <summary>
        /// Cached display name of the GameObject.
        /// </summary>
        public string GameObjectName;

        /// <summary>
        /// Hierarchy full path of the GameObject (e.g. "Canvas/Panel/Button").
        /// </summary>
        public string GameObjectPath;

        /// <summary>
        /// Asset path of the scene containing the favorited GameObject.
        /// </summary>
        public string ScenePath;

        /// <summary>
        /// Display-friendly scene name.
        /// </summary>
        public string SceneName;

        /// <summary>
        /// User-defined memo for the favorite entry.
        /// </summary>
        public string Memo;

        /// <summary>
        /// Whether the GameObject could not be resolved (deleted or scene not loaded).
        /// </summary>
        public bool IsMissing;
    }

    /// <summary>
    /// Persistent data container for Hierarchy favorites.
    /// Stored as JSON file under Application.persistentDataPath.
    /// </summary>
    [Serializable]
    public class HierarchyFavoriteData
    {
        /// <summary>
        /// All favorite entries.
        /// </summary>
        public List<FavoriteEntry> Entries = new();

        private static string BuildSaveFilePath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "UniLab",
                "Editor",
                "HierarchyFavorite.json");
        }

        /// <summary>
        /// Loads favorite data from the project-local JSON file.
        /// </summary>
        public static HierarchyFavoriteData Load()
        {
            var filePath = BuildSaveFilePath();
            if (!File.Exists(filePath))
            {
                return new HierarchyFavoriteData();
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var data = JsonUtility.FromJson<HierarchyFavoriteData>(json);
                return data ?? new HierarchyFavoriteData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[HierarchyFavorite] Failed to load data: {exception.Message}");
                return new HierarchyFavoriteData();
            }
        }

        /// <summary>
        /// Saves the favorite data to the project-local JSON file.
        /// </summary>
        public static void Save(HierarchyFavoriteData data)
        {
            var filePath = BuildSaveFilePath();
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Adds an entry if no existing entry shares the same GlobalObjectIdString.
        /// </summary>
        public static void AddEntry(FavoriteEntry entry)
        {
            var data = Load();

            for (int i = 0; i < data.Entries.Count; i++)
            {
                if (data.Entries[i].GlobalObjectIdString == entry.GlobalObjectIdString)
                {
                    return;
                }
            }

            data.Entries.Add(entry);
            Save(data);
        }
    }
}
