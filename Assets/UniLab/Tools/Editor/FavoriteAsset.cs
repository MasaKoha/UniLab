using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UniLab.Tools.Editor
{
    public class FavoriteAssetsWindow : EditorWindow
    {
        private const string ProjectPrefsKey = "FavoriteAssetsWindow.Favorites.Project";
        private List<string> _favorites = new(); // GUIDs
        private Vector2 _scroll;
        private ReorderableList _reorderableList;

        [MenuItem("UniLab/Tools/Favorite Assets Window")]
        public static void ShowWindow()
        {
            GetWindow<FavoriteAssetsWindow>("Favorite Assets Window");
        }

        private void OnEnable()
        {
            LoadFavorites();
            _reorderableList = new ReorderableList(_favorites, typeof(string), true, false, false, false);
            _reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var guid = _favorites[index];
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (asset != null)
                {
                    var icon = AssetDatabase.GetCachedIcon(path) ?? EditorGUIUtility.IconContent("d_DefaultAsset Icon").image;
                    var content = new GUIContent(asset.name, icon);

                    var buttonRect = new Rect(rect.x, rect.y, rect.width - 80, 20);
                    if (GUI.Button(buttonRect, content, EditorStyles.miniButtonLeft))
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }

                    var openRect = new Rect(rect.x + rect.width - 80, rect.y, 40, 20);
                    if (GUI.Button(openRect, "開く"))
                    {
                        AssetDatabase.OpenAsset(asset);
                    }

                    var delRect = new Rect(rect.x + rect.width - 40, rect.y, 30, 20);
                    if (GUI.Button(delRect, "🗑"))
                    {
                        _favorites.RemoveAt(index);
                        SaveFavorites();
                        GUIUtility.ExitGUI();
                    }
                }
                else
                {
                    var labelRect = new Rect(rect.x, rect.y, rect.width - 40, 20);
                    EditorGUI.LabelField(labelRect, "(見つかりません)");

                    var delRect = new Rect(rect.x + rect.width - 40, rect.y, 30, 20);
                    if (GUI.Button(delRect, "🗑"))
                    {
                        _favorites.RemoveAt(index);
                        SaveFavorites();
                        GUIUtility.ExitGUI();
                    }
                }
            };
            _reorderableList.onReorderCallback = (list) => SaveFavorites();
            _reorderableList.elementHeight = 22;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("アセットをここにドラッグして登録", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            // ドロップ領域
            var dropRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "ここにドロップ");

            // 右側に全てクリアボタン
            if (GUILayout.Button("全てクリア", GUILayout.Height(40), GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("確認", "お気に入りを全て削除しますか？", "はい", "いいえ"))
                {
                    _favorites.Clear();
                    SaveFavorites();
                }
            }

            GUILayout.EndHorizontal();

            // ドラッグ＆ドロップ処理
            var evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (dropRect.Contains(evt.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            var path = AssetDatabase.GetAssetPath(obj);
                            if (!string.IsNullOrEmpty(path))
                            {
                                var guid = AssetDatabase.AssetPathToGUID(path);
                                if (!string.IsNullOrEmpty(guid) && !_favorites.Contains(guid))
                                {
                                    _favorites.Add(guid);
                                }
                            }
                        }

                        SaveFavorites();
                        evt.Use();
                    }
                }
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("お気に入り一覧", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(250));
            if (_reorderableList != null)
            {
                _reorderableList.DoLayoutList();
            }

            EditorGUILayout.EndScrollView();
        }

        private void LoadFavorites()
        {
            var joined = EditorUserSettings.GetConfigValue(ProjectPrefsKey);
            _favorites = string.IsNullOrEmpty(joined) ? new List<string>() : joined.Split('|').ToList();

            // pathベースからGUIDベースへの自動マイグレーション
            var needsMigration = false;
            for (int i = 0; i < _favorites.Count; i++)
            {
                // GUIDは32文字の16進数文字列、pathは通常"Assets/"で始まる
                if (_favorites[i].StartsWith("Assets/") || _favorites[i].StartsWith("Packages/"))
                {
                    var guid = AssetDatabase.AssetPathToGUID(_favorites[i]);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        _favorites[i] = guid;
                        needsMigration = true;
                    }
                }
            }

            if (needsMigration)
            {
                SaveFavorites();
            }
        }

        private void SaveFavorites()
        {
            EditorUserSettings.SetConfigValue(ProjectPrefsKey, string.Join("|", _favorites));
        }
    }
}