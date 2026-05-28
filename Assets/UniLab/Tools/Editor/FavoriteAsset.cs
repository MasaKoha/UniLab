using System;
using System.Collections.Generic;
using System.IO;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UniLab.Tools.Editor
{
    /// <summary>
    /// Editor window for managing favorite assets with drag-reorderable list.
    /// </summary>
    public class FavoriteAssetsWindow : EditorWindow
    {
        private const float DropAreaHeight = 40f;
        private const float ClearButtonWidth = 100f;
        private const float OpenButtonWidth = 40f;
        private const float DeleteButtonWidth = 20f;
        private const float ElementSpacing = 4f;

        private string _saveFilePath = string.Empty;

        [Serializable]
        private class FavoriteEntry
        {
            public string Guid;
        }

        [Serializable]
        private class FavoriteAssetsData
        {
            public List<FavoriteEntry> Entries = new();
        }

        private List<FavoriteEntry> _entries = new();
        private Vector2 _scrollPosition;
        private ReorderableList _reorderableList;

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

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            _reorderableList?.DoLayoutList();
            EditorGUILayout.EndScrollView();
        }

        private void RebuildReorderableList()
        {
            _reorderableList = new ReorderableList(_entries, typeof(FavoriteEntry), true, true, false, false);
            _reorderableList.drawHeaderCallback = DrawListHeader;
            _reorderableList.drawElementCallback = DrawListElement;
            _reorderableList.onReorderCallback = OnListReorder;
        }

        private void DrawListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, EditorToolLabels.Get(LabelKey.FavoriteAssetList));
        }

        private void DrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _entries.Count)
            {
                return;
            }

            var entry = _entries[index];
            var path = AssetDatabase.GUIDToAssetPath(entry.Guid);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            // Why: shrink rect vertically to avoid overlapping adjacent elements
            rect.y += 2f;
            rect.height -= 4f;

            if (asset == null)
            {
                DrawNotFoundElement(rect, path, entry);
                return;
            }

            DrawAssetElement(rect, path, asset, entry);
        }

        private void DrawNotFoundElement(Rect rect, string path, FavoriteEntry entry)
        {
            var nameWidth = rect.width - DeleteButtonWidth - ElementSpacing;
            var nameRect = new Rect(rect.x, rect.y, nameWidth, rect.height);
            var deleteRect = new Rect(nameRect.xMax + ElementSpacing, rect.y, DeleteButtonWidth, rect.height);

            // Why: asset may exist on disk but fail to load (e.g. missing script reference)
            var displayName = string.IsNullOrEmpty(path)
                ? EditorToolLabels.Get(LabelKey.NotFound)
                : Path.GetFileNameWithoutExtension(path) + " " + EditorToolLabels.Get(LabelKey.Missing);
            EditorGUI.LabelField(nameRect, displayName);

            if (GUI.Button(deleteRect, "\u00d7"))
            {
                RemoveEntry(entry);
                SaveFavorites();
                RebuildReorderableList();
            }
        }

        private void DrawAssetElement(Rect rect, string path, UnityEngine.Object asset, FavoriteEntry entry)
        {
            var nameWidth = rect.width - OpenButtonWidth - DeleteButtonWidth - ElementSpacing * 2;
            var nameRect = new Rect(rect.x, rect.y, nameWidth, rect.height);
            var openRect = new Rect(nameRect.xMax + ElementSpacing, rect.y, OpenButtonWidth, rect.height);
            var deleteRect = new Rect(openRect.xMax + ElementSpacing, rect.y, DeleteButtonWidth, rect.height);

            // --- Asset name button with icon ---
            var icon = AssetDatabase.GetCachedIcon(path) ?? EditorGUIUtility.IconContent("d_DefaultAsset Icon").image;
            // Why: some asset types (asmdef, RenderPipelineGlobalSettings, etc.) return empty Object.name
            var displayName = string.IsNullOrEmpty(asset.name) ? Path.GetFileNameWithoutExtension(path) : asset.name;
            var content = new GUIContent(displayName, icon);
            if (GUI.Button(nameRect, content, EditorStyles.miniButtonLeft))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            // --- Open button ---
            if (GUI.Button(openRect, EditorToolLabels.Get(LabelKey.Open), EditorStyles.miniButtonMid))
            {
                AssetDatabase.OpenAsset(asset);
            }

            // --- Delete button ---
            if (GUI.Button(deleteRect, "\u00d7", EditorStyles.miniButtonRight))
            {
                RemoveEntry(entry);
                SaveFavorites();
                RebuildReorderableList();
            }
        }

        private void OnListReorder(ReorderableList list)
        {
            SaveFavorites();
        }

        private void RemoveEntry(FavoriteEntry entry)
        {
            _entries.Remove(entry);
        }

        private void LoadFavorites()
        {
            _entries = LoadEntriesFromFile();
            RebuildReorderableList();
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
        }

        private void ClearFavoritesIfConfirmed()
        {
            if (!EditorUtility.DisplayDialog(
                    EditorToolLabels.Get(LabelKey.Confirm),
                    EditorToolLabels.Get(LabelKey.ConfirmClearFavorites),
                    EditorToolLabels.Get(LabelKey.Yes),
                    EditorToolLabels.Get(LabelKey.No)))
            {
                return;
            }

            _entries.Clear();
            SaveFavorites();
            RebuildReorderableList();
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

            // Why: use paths instead of objectReferences so assets with missing scripts can be registered
            foreach (var dragPath in DragAndDrop.paths)
            {
                if (string.IsNullOrEmpty(dragPath))
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(dragPath);
                if (string.IsNullOrEmpty(guid) || ContainsGuid(guid))
                {
                    continue;
                }

                _entries.Add(new FavoriteEntry { Guid = guid });
            }

            SaveFavorites();
            RebuildReorderableList();
            evt.Use();
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
                var data = JsonUtility.FromJson<FavoriteAssetsData>(json);
                if (data?.Entries != null)
                {
                    return data.Entries;
                }

                return new List<FavoriteEntry>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FavoriteAssets] Failed to load favorites: {exception.Message}");
                return new List<FavoriteEntry>();
            }
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
