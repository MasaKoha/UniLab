using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UniLab.Tools.Editor
{
    public class FavoriteAssetsWindow : EditorWindow
    {
        private const float _dropAreaHeight = 40f;
        private const float _clearButtonWidth = 100f;
        private const float _listAreaHeight = 250f;
        private const float _elementHeight = 22f;
        private const float _openButtonWidth = 40f;
        private const float _deleteButtonWidth = 30f;
        private const float _rightButtonsWidth = 80f;

        private string _saveFilePath = string.Empty;

        [Serializable]
        private class FavoriteAssetsData
        {
            public List<string> Favorites = new();
        }

        private List<string> _favorites = new();
        private Vector2 _scroll;
        private ReorderableList _reorderableList;

        [MenuItem("UniLab/Tools/Favorite Assets Window")]
        public static void ShowWindow()
        {
            GetWindow<FavoriteAssetsWindow>("Favorite Assets Window");
        }

        private void OnEnable()
        {
            _saveFilePath = BuildSaveFilePath();
            EnsureSaveFileExists();
            LoadFavorites();
            InitializeReorderableList();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("アセットをここにドラッグして登録", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            var dropRect = DrawDropArea();
            DrawClearButton();
            GUILayout.EndHorizontal();

            HandleDragAndDrop(dropRect, Event.current);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("お気に入り一覧", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(_listAreaHeight));
            _reorderableList?.DoLayoutList();
            EditorGUILayout.EndScrollView();
        }

        private void LoadFavorites()
        {
            _favorites = LoadFavoritesFromFile();
            if (MigratePathEntriesToGuids())
            {
                SaveFavorites();
            }
        }

        private void SaveFavorites()
        {
            var data = new FavoriteAssetsData { Favorites = new List<string>(_favorites) };
            var json = JsonUtility.ToJson(data);
            var directory = Path.GetDirectoryName(_saveFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_saveFilePath, json);
        }

        private void DrawFavoriteElement(Rect rect, int index)
        {
            var guid = _favorites[index];
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            var delRect = new Rect(rect.x + rect.width - _deleteButtonWidth, rect.y, _deleteButtonWidth, 20);

            if (asset == null)
            {
                var labelRect = new Rect(rect.x, rect.y, rect.width - _deleteButtonWidth, 20);
                EditorGUI.LabelField(labelRect, "(見つかりません)");
                DrawDeleteButton(delRect, index);
                return;
            }

            var icon = AssetDatabase.GetCachedIcon(path) ?? EditorGUIUtility.IconContent("d_DefaultAsset Icon").image;
            var content = new GUIContent(asset.name, icon);
            var buttonRect = new Rect(rect.x, rect.y, rect.width - _rightButtonsWidth, 20);
            if (GUI.Button(buttonRect, content, EditorStyles.miniButtonLeft))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            var openRect = new Rect(rect.x + rect.width - _rightButtonsWidth, rect.y, _openButtonWidth, 20);
            if (GUI.Button(openRect, "開く"))
            {
                AssetDatabase.OpenAsset(asset);
            }

            DrawDeleteButton(delRect, index);
        }

        private void DrawDeleteButton(Rect rect, int index)
        {
            if (!GUI.Button(rect, "🗑"))
            {
                return;
            }

            _favorites.RemoveAt(index);
            SaveFavorites();
            GUIUtility.ExitGUI();
        }

        private void ClearFavoritesIfConfirmed()
        {
            if (!EditorUtility.DisplayDialog("確認", "お気に入りを全て削除しますか？", "はい", "いいえ"))
            {
                return;
            }

            _favorites.Clear();
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
            foreach (var obj in DragAndDrop.objectReferences)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid) || _favorites.Contains(guid))
                {
                    continue;
                }

                _favorites.Add(guid);
            }

            SaveFavorites();
            evt.Use();
        }

        private List<string> LoadFavoritesFromFile()
        {
            if (!File.Exists(_saveFilePath))
            {
                return new List<string>();
            }

            try
            {
                var json = File.ReadAllText(_saveFilePath);
                var data = JsonUtility.FromJson<FavoriteAssetsData>(json);
                return data?.Favorites ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private bool MigratePathEntriesToGuids()
        {
            var migrated = false;
            for (var i = 0; i < _favorites.Count; i++)
            {
                var favorite = _favorites[i];
                if (!favorite.StartsWith("Assets/") && !favorite.StartsWith("Packages/"))
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(favorite);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                _favorites[i] = guid;
                migrated = true;
            }

            return migrated;
        }

        private void InitializeReorderableList()
        {
            _reorderableList = new ReorderableList(_favorites, typeof(string), true, false, false, false)
            {
                drawElementCallback = (rect, index, _, _) => DrawFavoriteElement(rect, index),
                onReorderCallback = _ => SaveFavorites(),
                elementHeight = _elementHeight
            };
        }

        private Rect DrawDropArea()
        {
            var dropRect = GUILayoutUtility.GetRect(0, _dropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "ここにドロップ");
            return dropRect;
        }

        private void DrawClearButton()
        {
            if (!GUILayout.Button("全てクリア", GUILayout.Height(_dropAreaHeight), GUILayout.Width(_clearButtonWidth)))
            {
                return;
            }

            ClearFavoritesIfConfirmed();
        }

        private string BuildSaveFilePath()
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

            _favorites = new List<string>();
            SaveFavorites();
        }
    }
}
