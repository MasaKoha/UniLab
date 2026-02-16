using System.Collections.Generic;
using System.IO;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.Tools.Editor.MissingChecker
{
    public class ProjectMissingCheckerWindow : EditorWindow
    {
        private readonly List<string> _missingPaths = new();
        private Vector2 _scroll;
        private bool _isScanning;
        private ProjectMissingCheckerSettings _settings;
        private const string _settingsAssetPath = "Assets/Generated/UniCore/MissingCheckerSettings.asset";

        [MenuItem("UniLab/Tools/Project Missing Checker/Open Window")]
        public static void ShowWindow()
        {
            GetWindow<ProjectMissingCheckerWindow>("Missing Checker");
        }

        [MenuItem("UniLab/Tools/Project Missing Checker/Settings")]
        public static void ShowSettings()
        {
            var settings = ProjectMissingCheckerSettings.GetOrCreate();
            FocusSettingsAsset(settings);
        }

        private void OnEnable()
        {
            _settings = ProjectMissingCheckerSettings.GetOrCreate();
            ProjectMissingCheckerSettings.SetActive(_settings);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("全アセットの Missing 参照をチェック", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            DrawSettingsUI();

            using (new EditorGUI.DisabledScope(_isScanning))
            {
                if (_settings == null)
                {
                    EditorGUILayout.HelpBox("設定ファイルが見つかりません。先に設定ファイルを作成してください。", MessageType.Warning);
                }

                if (GUILayout.Button("全アセットをスキャン"))
                {
                    if (_settings != null)
                    {
                        ScanAllAssets();
                    }
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Missing 検出数: {_missingPaths.Count}");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var path in _missingPaths)
            {
                EditorGUILayout.BeginHorizontal();
                var fileName = System.IO.Path.GetFileName(path);
                if (GUILayout.Button(fileName, EditorStyles.miniButtonLeft))
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }

                if (GUILayout.Button("開く", EditorStyles.miniButtonRight, GUILayout.Width(50)))
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj != null)
                    {
                        AssetDatabase.OpenAsset(obj);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsUI()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("設定ファイル", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _settings = (ProjectMissingCheckerSettings)EditorGUILayout.ObjectField(
                "Settings",
                _settings,
                typeof(ProjectMissingCheckerSettings),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                ProjectMissingCheckerSettings.SetActive(_settings);
            }

            if (_settings != null)
            {
                var next = EditorGUILayout.ToggleLeft("ヒエラルキー色付けを有効", _settings.EnableHierarchyHighlight);
                if (next != _settings.EnableHierarchyHighlight)
                {
                    _settings.EnableHierarchyHighlight = next;
                    _settings.SaveAsset();
                }
            }

            var dropRect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "ここに設定ファイルをドロップ");
            HandleSettingsDrop(dropRect);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("設定を開く"))
            {
                if (_settings != null)
                {
                    FocusSettingsAsset(_settings);
                }
                else
                {
                    ShowSettings();
                }
            }

            if (GUILayout.Button("設定ファイル作成"))
            {
                _settings = CreateOrSelectSettingsAsset();
                ProjectMissingCheckerSettings.SetActive(_settings);
                Selection.activeObject = _settings;
                EditorGUIUtility.PingObject(_settings);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private static void FocusSettingsAsset(ProjectMissingCheckerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            ProjectMissingCheckerSettings.SetActive(settings);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }


        private void HandleSettingsDrop(Rect dropRect)
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return;
            }

            if (!dropRect.Contains(evt.mousePosition))
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is ProjectMissingCheckerSettings settings)
                    {
                        _settings = settings;
                        break;
                    }
                }

                evt.Use();
            }
        }

        private static ProjectMissingCheckerSettings CreateOrSelectSettingsAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ProjectMissingCheckerSettings>(_settingsAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var settings = CreateInstance<ProjectMissingCheckerSettings>();
            var targetFolder = GetActiveProjectFolderPath();
            if (string.IsNullOrEmpty(targetFolder) || !targetFolder.StartsWith("Assets"))
            {
                targetFolder = "Assets/Generated/UniCore";
            }

            EnsureFolderExists(targetFolder);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(targetFolder, "MissingCheckerSettings.asset"));
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private void ScanAllAssets()
        {
            _missingPaths.Clear();
            _isScanning = true;

            try
            {
                if (_settings == null)
                {
                    _settings = ProjectMissingCheckerSettings.GetOrCreate();
                }

                var extensionFilter = ProjectScanFilterUtility.BuildExtensionFilter(_settings.ExtensionsCsv);
                var folderRoots = ProjectScanFilterUtility.BuildFolderRoots(_settings.TargetFolders);
                var missingSelfGuids = new List<string>();
                var missingParentGuids = new HashSet<string>();
                var guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                    {
                        continue;
                    }

                    if (!ProjectScanFilterUtility.PassExtensionFilter(path, extensionFilter))
                    {
                        continue;
                    }

                    if (!ProjectScanFilterUtility.PassFolderFilter(path, folderRoots))
                    {
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Missing Checker",
                            path,
                            (float)i / guids.Length))
                    {
                        break;
                    }

                    if (HasMissingReferencesAtPath(path))
                    {
                        _missingPaths.Add(path);
                        missingSelfGuids.Add(guids[i]);
                        CollectParentFolderGuids(path, missingParentGuids);
                    }
                }

                ProjectMissingCheckerProjectHighlighter.SetMissingGuids(missingSelfGuids, missingParentGuids);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
            }
        }

        private static bool HasMissingReferencesAtPath(string path)
        {
            if (path.EndsWith(".unity"))
            {
                return HasMissingReferencesInScene(path);
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                if (asset == null)
                {
                    return true;
                }

                if (asset is GameObject prefabRoot)
                {
                    if (HasMissingReferences(prefabRoot))
                    {
                        return true;
                    }
                }
                else
                {
                    if (HasMissingReferences(asset))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasMissingReferencesInScene(string path)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var openedAdditively = false;

            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (HasMissingReferences(root))
                    {
                        return true;
                    }
                }
            }
            finally
            {
                if (openedAdditively)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return false;
        }

        private static bool HasMissingReferences(GameObject go)
        {
            var components = go.GetComponentsInChildren<Component>(true);
            foreach (var component in components)
            {
                if (component == null)
                {
                    return true;
                }

                if (HasMissingReferences(component))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMissingReferences(Object obj)
        {
            var serializedObject = new SerializedObject(obj);
            var iterator = serializedObject.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectParentFolderGuids(string assetPath, HashSet<string> parentGuids)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            var folder = System.IO.Path.GetDirectoryName(assetPath);
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

                var next = System.IO.Path.GetDirectoryName(folder);
                if (string.IsNullOrEmpty(next))
                {
                    break;
                }

                folder = next.Replace('\\', '/');
            }
        }

        private static string GetActiveProjectFolderPath()
        {
            var selectedFolder = GetSelectedProjectFolderPath();
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                return selectedFolder;
            }

            var reflected = TryGetProjectWindowFolderByReflection();
            if (!string.IsNullOrEmpty(reflected))
            {
                return reflected;
            }

            var active = Selection.activeObject;
            if (active == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(active);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            var directory = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(directory) ? null : directory.Replace('\\', '/');
        }

        private static string GetSelectedProjectFolderPath()
        {
            var guids = Selection.assetGUIDs;
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static string TryGetProjectWindowFolderByReflection()
        {
            var utilType = typeof(ProjectWindowUtil);
            var method = utilType.GetMethod("GetActiveFolderPath",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                return null;
            }

            var result = method.Invoke(null, null) as string;
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }

            return result.Replace('\\', '/');
        }

        private static void EnsureFolderExists(string folderPath)
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
    }
}
