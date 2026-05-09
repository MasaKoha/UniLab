using System;
using System.Collections.Generic;
using System.IO;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor
{
    /// <summary>
    /// Editor window for managing favorite assets with category grouping.
    /// </summary>
    public class FavoriteAssetsWindow : EditorWindow
    {
        private const float DropAreaHeight = 40f;
        private const float ClearButtonWidth = 100f;
        private const float OpenButtonWidth = 40f;
        private const float DeleteButtonWidth = 30f;
        private const float CategoryDropdownWidth = 100f;
        private static string DefaultCategory => EditorToolLabels.Get(LabelKey.DefaultCategory);

        private string _saveFilePath = string.Empty;

        [Serializable]
        private class FavoriteEntry
        {
            public string Guid;
            public string Category;
        }

        [Serializable]
        private class FavoriteAssetsData
        {
            public List<FavoriteEntry> Entries = new();
        }

        /// <summary>
        /// Legacy data format for backward compatibility.
        /// Only used during migration from the old Favorites (GUID-only) format.
        /// </summary>
        [Serializable]
        private class LegacyFavoriteAssetsData
        {
            public List<string> Favorites = new();
        }

        private List<FavoriteEntry> _entries = new();
        private Vector2 _scrollPosition;
        private readonly Dictionary<string, bool> _categoryFoldouts = new();
        private string _newCategoryName = string.Empty;
        private int _selectedCategoryIndex;
        private bool _isCategoryCacheDirty = true;
        private string[] _cachedCategoryNames = System.Array.Empty<string>();
        private Dictionary<string, List<FavoriteEntry>> _cachedEntriesByCategory = new();

        /// <summary>
        /// Opens the favorite assets window.
        /// </summary>
        [MenuItem("UniLab/Tools/Asset Favorite/Open Window")]
        public static void ShowWindow()
        {
            GetWindow<FavoriteAssetsWindow>("Favorite Assets Window");
        }

        private void OnEnable()
        {
            _saveFilePath = BuildSaveFilePath();
            EnsureSaveFileExists();
            LoadFavorites();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.FavoriteAssetDragHint), EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            var dropRect = DrawDropArea();
            DrawClearButton();
            GUILayout.EndHorizontal();

            HandleDragAndDrop(dropRect, Event.current);

            GUILayout.Space(6);
            DrawCategoryToolbar();
            GUILayout.Space(6);

            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.FavoriteAssetList), EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawCategorizedList();
            EditorGUILayout.EndScrollView();
        }

        private void DrawCategoryToolbar()
        {
            RebuildCategoryCacheIfNeeded();
            EditorGUILayout.BeginVertical("box");

            // --- Target category for drag-and-drop registration ---
            _selectedCategoryIndex = ClampCategoryIndex(_selectedCategoryIndex, _cachedCategoryNames.Length);
            _selectedCategoryIndex = EditorGUILayout.Popup(EditorToolLabels.Get(LabelKey.TargetCategory), _selectedCategoryIndex, _cachedCategoryNames);

            // --- Add new category ---
            EditorGUILayout.BeginHorizontal();
            _newCategoryName = EditorGUILayout.TextField(EditorToolLabels.Get(LabelKey.NewCategory), _newCategoryName);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newCategoryName)))
            {
                if (GUILayout.Button(EditorToolLabels.Get(LabelKey.Add), GUILayout.Width(60)))
                {
                    AddNewCategory();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void AddNewCategory()
        {
            var trimmed = _newCategoryName.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            // Register in foldouts so the category is visible even before any entries are assigned
            _categoryFoldouts[trimmed] = true;
            _newCategoryName = string.Empty;
            _isCategoryCacheDirty = true;

            // Auto-select the newly created category as the drop target
            RebuildCategoryCacheIfNeeded();
            _selectedCategoryIndex = System.Array.IndexOf(_cachedCategoryNames, trimmed);
            if (_selectedCategoryIndex < 0)
            {
                _selectedCategoryIndex = 0;
            }

            GUI.FocusControl(null);
        }

        private void DrawCategorizedList()
        {
            RebuildCategoryCacheIfNeeded();
            var needsSave = false;

            for (int categoryIndex = 0; categoryIndex < _cachedCategoryNames.Length; categoryIndex++)
            {
                var category = _cachedCategoryNames[categoryIndex];
                if (!_categoryFoldouts.ContainsKey(category))
                {
                    _categoryFoldouts[category] = true;
                }

                _cachedEntriesByCategory.TryGetValue(category, out var entriesInCategory);
                var entryCount = entriesInCategory != null ? entriesInCategory.Count : 0;
                var headerLabel = category + " (" + entryCount + ")";
                _categoryFoldouts[category] = EditorGUILayout.Foldout(_categoryFoldouts[category], headerLabel, true);

                if (!_categoryFoldouts[category] || entriesInCategory == null)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                for (int entryIndex = 0; entryIndex < entriesInCategory.Count; entryIndex++)
                {
                    if (DrawFavoriteEntryRow(entriesInCategory[entryIndex], _cachedCategoryNames))
                    {
                        needsSave = true;
                    }
                }

                EditorGUI.indentLevel--;
            }

            if (needsSave)
            {
                SaveFavorites();
            }
        }

        /// <summary>
        /// Draws a single favorite entry row.
        /// Returns true if data was modified and needs saving.
        /// </summary>
        private bool DrawFavoriteEntryRow(FavoriteEntry entry, string[] categories)
        {
            var path = AssetDatabase.GUIDToAssetPath(entry.Guid);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            var modified = false;

            EditorGUILayout.BeginHorizontal();

            if (asset == null)
            {
                EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.NotFound));
                if (GUILayout.Button(EditorToolLabels.Get(LabelKey.Delete), GUILayout.Width(DeleteButtonWidth)))
                {
                    RemoveEntry(entry);
                    SaveFavorites();
                    GUIUtility.ExitGUI();
                    return true;
                }

                EditorGUILayout.EndHorizontal();
                return false;
            }

            // Asset name button
            var icon = AssetDatabase.GetCachedIcon(path) ?? EditorGUIUtility.IconContent("d_DefaultAsset Icon").image;
            var content = new GUIContent(asset.name, icon);
            if (GUILayout.Button(content, EditorStyles.miniButtonLeft))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            // Open button
            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.Open), EditorStyles.miniButtonMid, GUILayout.Width(OpenButtonWidth)))
            {
                AssetDatabase.OpenAsset(asset);
            }

            // Category change dropdown
            var currentCategoryIndex = System.Array.IndexOf(categories, entry.Category);
            if (currentCategoryIndex < 0)
            {
                currentCategoryIndex = 0;
            }

            var newCategoryIndex = EditorGUILayout.Popup(
                currentCategoryIndex,
                categories,
                GUILayout.Width(CategoryDropdownWidth));
            if (newCategoryIndex != currentCategoryIndex && newCategoryIndex >= 0 && newCategoryIndex < categories.Length)
            {
                entry.Category = categories[newCategoryIndex];
                modified = true;
                _isCategoryCacheDirty = true;
            }

            // Delete button
            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.Delete), EditorStyles.miniButtonRight, GUILayout.Width(DeleteButtonWidth)))
            {
                RemoveEntry(entry);
                SaveFavorites();
                GUIUtility.ExitGUI();
                return true;
            }

            EditorGUILayout.EndHorizontal();
            return modified;
        }

        private void RemoveEntry(FavoriteEntry entry)
        {
            _entries.Remove(entry);
        }

        private void LoadFavorites()
        {
            _entries = LoadEntriesFromFile();
            _isCategoryCacheDirty = true;
            if (MigratePathEntriesToGuids())
            {
                SaveFavorites();
            }
        }

        private void SaveFavorites()
        {
            var data = new FavoriteAssetsData { Entries = new List<FavoriteEntry>(_entries) };
            var json = JsonUtility.ToJson(data, true);
            var directory = Path.GetDirectoryName(_saveFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_saveFilePath, json);
            _isCategoryCacheDirty = true;
        }

        private void ClearFavoritesIfConfirmed()
        {
            if (!EditorUtility.DisplayDialog(EditorToolLabels.Get(LabelKey.Confirm), EditorToolLabels.Get(LabelKey.ConfirmClearFavorites), EditorToolLabels.Get(LabelKey.Yes), EditorToolLabels.Get(LabelKey.No)))
            {
                return;
            }

            _entries.Clear();
            _categoryFoldouts.Clear();
            SaveFavorites();
        }

        private void HandleDragAndDrop(Rect dropRect, Event evt)
        {
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return;
            }

            if (!dropRect.Contains(evt.mousePosition))
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type != EventType.DragPerform)
            {
                return;
            }

            DragAndDrop.AcceptDrag();
            var targetCategory = ResolveTargetCategory();

            foreach (var obj in DragAndDrop.objectReferences)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid) || ContainsGuid(guid))
                {
                    continue;
                }

                _entries.Add(new FavoriteEntry { Guid = guid, Category = targetCategory });
            }

            SaveFavorites();
            evt.Use();
        }

        private string ResolveTargetCategory()
        {
            var categories = CollectCategoriesIncludingFoldouts();
            if (_selectedCategoryIndex >= 0 && _selectedCategoryIndex < categories.Count)
            {
                return categories[_selectedCategoryIndex];
            }

            return DefaultCategory;
        }

        private bool ContainsGuid(string guid)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Guid == guid)
                {
                    return true;
                }
            }

            return false;
        }

        private List<FavoriteEntry> LoadEntriesFromFile()
        {
            if (!File.Exists(_saveFilePath))
            {
                return new List<FavoriteEntry>();
            }

            try
            {
                var json = File.ReadAllText(_saveFilePath);
                return TryLoadNewFormat(json) ?? MigrateFromLegacyFormat(json);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[FavoriteAssets] Failed to load favorites: {exception.Message}");
                return new List<FavoriteEntry>();
            }
        }

        private static List<FavoriteEntry> TryLoadNewFormat(string json)
        {
            var data = JsonUtility.FromJson<FavoriteAssetsData>(json);
            if (data?.Entries != null)
            {
                return data.Entries;
            }

            return null;
        }

        private static List<FavoriteEntry> MigrateFromLegacyFormat(string json)
        {
            var legacyData = JsonUtility.FromJson<LegacyFavoriteAssetsData>(json);
            if (legacyData?.Favorites == null || legacyData.Favorites.Count == 0)
            {
                return new List<FavoriteEntry>();
            }

            var entries = new List<FavoriteEntry>(legacyData.Favorites.Count);
            for (int i = 0; i < legacyData.Favorites.Count; i++)
            {
                var guidOrPath = legacyData.Favorites[i];
                if (string.IsNullOrEmpty(guidOrPath))
                {
                    continue;
                }

                entries.Add(new FavoriteEntry { Guid = guidOrPath, Category = DefaultCategory });
            }

            return entries;
        }

        /// <summary>
        /// Migrates entries that store asset paths (Assets/... or Packages/...) to GUIDs.
        /// Returns true if any migration occurred.
        /// </summary>
        private bool MigratePathEntriesToGuids()
        {
            var migrated = false;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!entry.Guid.StartsWith("Assets/") && !entry.Guid.StartsWith("Packages/"))
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(entry.Guid);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                entry.Guid = guid;
                migrated = true;
            }

            return migrated;
        }

        /// <summary>
        /// Collects unique category names from current entries, always including the default category.
        /// </summary>
        private List<string> CollectCategories()
        {
            var categories = new List<string> { DefaultCategory };
            for (int i = 0; i < _entries.Count; i++)
            {
                var category = _entries[i].Category;
                if (string.IsNullOrEmpty(category))
                {
                    continue;
                }

                if (!categories.Contains(category))
                {
                    categories.Add(category);
                }
            }

            return categories;
        }

        /// <summary>
        /// Collects categories from entries plus any categories that exist only in foldouts
        /// (e.g. newly created categories with no entries yet).
        /// </summary>
        private List<string> CollectCategoriesIncludingFoldouts()
        {
            var categories = CollectCategories();
            foreach (var foldoutCategory in _categoryFoldouts.Keys)
            {
                if (!categories.Contains(foldoutCategory))
                {
                    categories.Add(foldoutCategory);
                }
            }

            return categories;
        }

        private void RebuildCategoryCacheIfNeeded()
        {
            if (!_isCategoryCacheDirty)
            {
                return;
            }

            var categories = CollectCategoriesIncludingFoldouts();
            _cachedCategoryNames = categories.ToArray();

            _cachedEntriesByCategory.Clear();
            for (int i = 0; i < _entries.Count; i++)
            {
                var entryCategory = _entries[i].Category;
                if (string.IsNullOrEmpty(entryCategory))
                {
                    entryCategory = DefaultCategory;
                }

                if (!_cachedEntriesByCategory.TryGetValue(entryCategory, out var list))
                {
                    list = new List<FavoriteEntry>();
                    _cachedEntriesByCategory[entryCategory] = list;
                }

                list.Add(_entries[i]);
            }

            _isCategoryCacheDirty = false;
        }

        private static int ClampCategoryIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= count)
            {
                return count - 1;
            }

            return index;
        }

        private Rect DrawDropArea()
        {
            var dropRect = GUILayoutUtility.GetRect(0, DropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, EditorToolLabels.Get(LabelKey.DropHere));
            return dropRect;
        }

        private void DrawClearButton()
        {
            if (!GUILayout.Button(EditorToolLabels.Get(LabelKey.ClearAll), GUILayout.Height(DropAreaHeight), GUILayout.Width(ClearButtonWidth)))
            {
                return;
            }

            ClearFavoritesIfConfirmed();
        }

        private static string BuildSaveFilePath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "UniLab",
                "Editor",
                "FavoriteAssetsWindow.json");
        }

        private void EnsureSaveFileExists()
        {
            if (File.Exists(_saveFilePath))
            {
                return;
            }

            _entries = new List<FavoriteEntry>();
            SaveFavorites();
        }
    }
}
