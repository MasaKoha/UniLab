using System.Collections.Generic;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.Tools.Editor.HierarchyFavorite
{
    /// <summary>
    /// Editor window that displays and manages Hierarchy GameObject favorites grouped by scene.
    /// </summary>
    public class HierarchyFavoriteWindow : EditorWindow
    {
        private const float DropAreaHeight = 40f;
        private const float HeaderButtonWidth = 70f;
        private const float RowHeight = 20f;
        private const float GameObjectRatio = 0.55f;
        private const float MemoRatio = 0.35f;
        private const float DeleteRatio = 0.1f;
        private const double PathRefreshCooldownSeconds = 0.5;

        // Why: EditorStyles is not available during static constructor — lazy init required
        private static GUIStyle _buttonCentered;
        private static GUIStyle _buttonRight;

        private static GUIStyle ButtonCentered
        {
            get
            {
                if (_buttonCentered == null)
                {
                    _buttonCentered = new GUIStyle(EditorStyles.miniButton)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        clipping = TextClipping.Clip
                    };
                }

                return _buttonCentered;
            }
        }

        private static GUIStyle ButtonRight
        {
            get
            {
                if (_buttonRight == null)
                {
                    _buttonRight = new GUIStyle(EditorStyles.miniButton)
                    {
                        alignment = TextAnchor.MiddleRight,
                        clipping = TextClipping.Clip
                    };
                }

                return _buttonRight;
            }
        }

        private HierarchyFavoriteData _data;
        private Vector2 _scrollPosition;
        private Dictionary<string, bool> _sceneFoldoutStates = new();
        private bool _isGroupCacheDirty = true;
        private Dictionary<string, List<FavoriteEntry>> _cachedGroups = new();
        private string[] _cachedScenePaths = System.Array.Empty<string>();
        private bool _needsPathRefresh;
        private double _lastPathRefreshTime = -1;

        /// <summary>
        /// Singleton accessor for the open window. Used by ContextMenu to push data directly.
        /// </summary>
        public static HierarchyFavoriteWindow Instance { get; private set; }

        [MenuItem("UniLab/Tools/Hierarchy Favorite/Open Window")]
        public static void ShowWindow()
        {
            GetWindow<HierarchyFavoriteWindow>("Hierarchy Favorite");
        }

        private void OnEnable()
        {
            Instance = this;
            _data = HierarchyFavoriteData.Load();
            _isGroupCacheDirty = true;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Why: Hierarchy 変更時にお気に入りのパスを更新する。
        // Play 中は不要。デバウンスでスパイクを抑制。
        private void OnHierarchyChanged()
        {
            if (Application.isPlaying)
            {
                return;
            }

            _needsPathRefresh = true;
        }

        private void RefreshPathsIfNeeded()
        {
            if (!_needsPathRefresh)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now - _lastPathRefreshTime < PathRefreshCooldownSeconds)
            {
                return;
            }

            _lastPathRefreshTime = now;
            _needsPathRefresh = false;

            var changed = false;
            for (int i = 0; i < _data.Entries.Count; i++)
            {
                var entry = _data.Entries[i];
                if (!GlobalObjectId.TryParse(entry.GlobalObjectIdString, out var globalId))
                {
                    continue;
                }

                var resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                var gameObject = resolved as GameObject;

                if (gameObject == null)
                {
                    // Why: resolve 失敗 = オブジェクトが削除された or シーン未ロード
                    if (!entry.IsMissing)
                    {
                        entry.IsMissing = true;
                        changed = true;
                    }

                    continue;
                }

                var newPath = BuildGameObjectPath(gameObject.transform);
                var newName = gameObject.name;

                if (entry.IsMissing)
                {
                    entry.IsMissing = false;
                    changed = true;
                }

                if (entry.GameObjectPath != newPath || entry.GameObjectName != newName)
                {
                    entry.GameObjectPath = newPath;
                    entry.GameObjectName = newName;
                    changed = true;
                }
            }

            if (changed)
            {
                HierarchyFavoriteData.Save(_data);
                _isGroupCacheDirty = true;
                Repaint();
            }
        }

        private void OnGUI()
        {
            RebuildGroupCacheIfNeeded();
            RefreshPathsIfNeeded();

            DrawHeader();
            DrawDropArea();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawSceneGroups();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.HierarchyFavoriteTitle), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.Reload), GUILayout.Width(HeaderButtonWidth)))
            {
                _data = HierarchyFavoriteData.Load();
                _isGroupCacheDirty = true;
            }

            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.ClearAll), GUILayout.Width(HeaderButtonWidth)))
            {
                ClearAllIfConfirmed();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawDropArea()
        {
            var dropRect = GUILayoutUtility.GetRect(0, DropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, EditorToolLabels.Get(LabelKey.HierarchyFavoriteDragHint));

            var currentEvent = Event.current;
            if (dropRect.Contains(currentEvent.mousePosition) && currentEvent.type == EventType.DragUpdated)
            {
                var hasGameObject = false;
                for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                {
                    if (DragAndDrop.objectReferences[i] is GameObject)
                    {
                        hasGameObject = true;
                        break;
                    }
                }

                DragAndDrop.visualMode = hasGameObject ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                currentEvent.Use();
            }
            else if (dropRect.Contains(currentEvent.mousePosition) && currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                {
                    if (DragAndDrop.objectReferences[i] is GameObject gameObject)
                    {
                        AddFavoriteFromGameObject(gameObject);
                    }
                }

                currentEvent.Use();
            }

            GUILayout.Space(4);
        }

        private void AddFavoriteFromGameObject(GameObject gameObject)
        {
            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            if (globalId.identifierType == 0)
            {
                EditorUtility.DisplayDialog(
                    EditorToolLabels.Get(LabelKey.HierarchyFavoriteTitle),
                    EditorToolLabels.Get(LabelKey.SceneNotSavedMessage),
                    "OK");
                return;
            }

            var entry = new FavoriteEntry
            {
                GlobalObjectIdString = globalId.ToString(),
                GameObjectName = gameObject.name,
                GameObjectPath = BuildGameObjectPath(gameObject.transform),
                ScenePath = gameObject.scene.path,
                SceneName = gameObject.scene.name,
                Memo = string.Empty
            };

            AddFavoriteEntry(entry);
        }

        private static string BuildGameObjectPath(Transform target)
        {
            return ProjectScanEditorUtility.BuildGameObjectPath(target);
        }

        /// <summary>
        /// Adds a favorite entry to this window's data and refreshes immediately.
        /// </summary>
        public void AddFavoriteEntry(FavoriteEntry entry)
        {
            for (int i = 0; i < _data.Entries.Count; i++)
            {
                if (_data.Entries[i].GlobalObjectIdString == entry.GlobalObjectIdString)
                {
                    return;
                }
            }

            _data.Entries.Add(entry);
            HierarchyFavoriteData.Save(_data);
            _isGroupCacheDirty = true;
            Repaint();
        }

        private void DrawSceneGroups()
        {
            for (int sceneIndex = 0; sceneIndex < _cachedScenePaths.Length; sceneIndex++)
            {
                var scenePath = _cachedScenePaths[sceneIndex];
                if (!_cachedGroups.TryGetValue(scenePath, out var entries))
                {
                    continue;
                }

                if (entries.Count == 0)
                {
                    continue;
                }

                var sceneName = entries[0].SceneName;
                var isSceneLoaded = IsSceneLoaded(scenePath);

                if (!_sceneFoldoutStates.ContainsKey(scenePath))
                {
                    _sceneFoldoutStates[scenePath] = true;
                }

                var headerLabel = sceneName + " (" + entries.Count + ")";
                _sceneFoldoutStates[scenePath] = EditorGUILayout.Foldout(
                    _sceneFoldoutStates[scenePath], headerLabel, true);

                if (!_sceneFoldoutStates[scenePath])
                {
                    continue;
                }

                EditorGUI.indentLevel++;

                var totalWidth = position.width;
                var gameObjectWidth = totalWidth * GameObjectRatio;
                var memoWidth = totalWidth * MemoRatio;
                var deleteWidth = totalWidth * DeleteRatio;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("GameObject", EditorStyles.miniBoldLabel, GUILayout.Width(gameObjectWidth));
                EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.Memo), EditorStyles.miniBoldLabel, GUILayout.Width(memoWidth));
                EditorGUILayout.LabelField("", GUILayout.Width(deleteWidth));
                EditorGUILayout.EndHorizontal();

                using (new EditorGUI.DisabledGroupScope(!isSceneLoaded))
                {
                    for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                    {
                        DrawEntryRow(entries[entryIndex], isSceneLoaded);
                    }
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawEntryRow(FavoriteEntry entry, bool isSceneLoaded)
        {
            var displayPath = entry.IsMissing
                ? EditorToolLabels.Get(LabelKey.Missing)
                : !string.IsNullOrEmpty(entry.GameObjectPath) ? entry.GameObjectPath : entry.GameObjectName;
            var totalWidth = position.width;
            var gameObjectWidth = totalWidth * GameObjectRatio;
            var memoWidth = totalWidth * MemoRatio;
            var deleteWidth = totalWidth * DeleteRatio;

            EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

            var textWidth = ButtonCentered.CalcSize(new GUIContent(displayPath)).x;
            var buttonStyle = textWidth <= gameObjectWidth ? ButtonCentered : ButtonRight;

            if (GUILayout.Button(displayPath, buttonStyle,
                    GUILayout.Width(gameObjectWidth), GUILayout.Height(RowHeight)))
            {
                if (isSceneLoaded)
                {
                    FocusEntry(entry);
                }
            }

            var newMemo = EditorGUILayout.DelayedTextField(entry.Memo,
                GUILayout.Width(memoWidth), GUILayout.Height(RowHeight));
            if (newMemo != entry.Memo)
            {
                entry.Memo = newMemo;
                HierarchyFavoriteData.Save(_data);
            }

            if (GUILayout.Button("x", EditorStyles.miniButton,
                    GUILayout.Width(deleteWidth), GUILayout.Height(RowHeight)))
            {
                RemoveEntryAndRefresh(entry);
                return;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void FocusEntry(FavoriteEntry entry)
        {
            if (!GlobalObjectId.TryParse(entry.GlobalObjectIdString, out var globalId))
            {
                EditorUtility.DisplayDialog(
                    EditorToolLabels.Get(LabelKey.HierarchyFavoriteTitle),
                    EditorToolLabels.Get(LabelKey.ParseFailedMessage),
                    "OK");
                return;
            }

            var resolvedObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
            if (resolvedObject == null)
            {
                EditorUtility.DisplayDialog(
                    EditorToolLabels.Get(LabelKey.HierarchyFavoriteTitle),
                    EditorToolLabels.Get(LabelKey.EntryNotFoundMessage),
                    "OK");
                return;
            }

            var gameObject = resolvedObject as GameObject;
            if (gameObject != null)
            {
                Selection.activeGameObject = gameObject;
                EditorGUIUtility.PingObject(gameObject);
            }
            else
            {
                Selection.activeObject = resolvedObject;
                EditorGUIUtility.PingObject(resolvedObject);
            }
        }

        private void RemoveEntryAndRefresh(FavoriteEntry entry)
        {
            _data.Entries.Remove(entry);
            HierarchyFavoriteData.Save(_data);
            _isGroupCacheDirty = true;
            GUIUtility.ExitGUI();
        }

        private void ClearAllIfConfirmed()
        {
            if (!EditorUtility.DisplayDialog(
                    EditorToolLabels.Get(LabelKey.HierarchyFavoriteTitle),
                    EditorToolLabels.Get(LabelKey.ConfirmClearAllBookmarks),
                    EditorToolLabels.Get(LabelKey.Yes), EditorToolLabels.Get(LabelKey.No)))
            {
                return;
            }

            _data.Entries.Clear();
            HierarchyFavoriteData.Save(_data);
            _sceneFoldoutStates.Clear();
            _isGroupCacheDirty = true;
        }

        private void RebuildGroupCacheIfNeeded()
        {
            if (!_isGroupCacheDirty)
            {
                return;
            }

            _cachedGroups.Clear();
            var scenePathOrder = new List<string>();

            for (int i = 0; i < _data.Entries.Count; i++)
            {
                var entry = _data.Entries[i];
                var scenePath = entry.ScenePath ?? string.Empty;

                if (!_cachedGroups.TryGetValue(scenePath, out var list))
                {
                    list = new List<FavoriteEntry>();
                    _cachedGroups[scenePath] = list;
                    scenePathOrder.Add(scenePath);
                }

                list.Add(entry);
            }

            _cachedScenePaths = scenePathOrder.ToArray();
            _isGroupCacheDirty = false;
        }

        private static bool IsSceneLoaded(string scenePath)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            return scene.IsValid() && scene.isLoaded;
        }
    }
}
