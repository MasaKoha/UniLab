using System.Collections.Generic;
using System.IO;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniLab.Tools.Editor.AssetReferenceFinder
{
    public class ProjectAssetReferenceFinderWindow : EditorWindow
    {
        private readonly List<string> _referencePaths = new();
        private Object _targetAsset;
        private Vector2 _scroll;
        private bool _isScanning;
        private AssetReferenceFinderSettings _settings;

        [MenuItem("UniLab/Tools/Project Asset Reference Finder/Open Window")]
        public static void ShowWindow()
        {
            GetWindow<ProjectAssetReferenceFinderWindow>("Asset Ref Finder");
        }

        [MenuItem("UniLab/Tools/Project Asset Reference Finder/Settings")]
        public static void ShowSettings()
        {
            var settings = AssetReferenceFinderSettings.GetOrCreate();
            FocusSettingsAsset(settings);
        }

        [MenuItem("Assets/UniLab/Find References In Project", false, 2000)]
        public static void FindFromSelectionMenu()
        {
            var window = GetWindow<ProjectAssetReferenceFinderWindow>("Asset Ref Finder");
            window.SetTargetFromSelection();
            window.ScanReferences();
        }

        [MenuItem("Assets/UniLab/Find References In Project", true)]
        private static bool ValidateFindFromSelectionMenu()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return !AssetDatabase.IsValidFolder(path);
        }

        private void OnEnable()
        {
            _settings = AssetReferenceFinderSettings.GetOrCreate();
            AssetReferenceFinderSettings.SetActive(_settings);
            SetTargetFromSelection();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("特定アセットの参照元を検索", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            DrawSettingsUI();
            EditorGUILayout.Space(4);

            _targetAsset = EditorGUILayout.ObjectField("参照先アセット", _targetAsset, typeof(Object), false);

            if (!CanScanTarget())
            {
                EditorGUILayout.HelpBox("Project 内のアセットを 1 つ指定してください。", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(_isScanning || !CanScanTarget()))
            {
                if (GUILayout.Button("参照元を検索"))
                {
                    ScanReferences();
                }
            }

            using (new EditorGUI.DisabledScope(_isScanning))
            {
                if (GUILayout.Button("ハイライト解除"))
                {
                    _referencePaths.Clear();
                    ProjectAssetReferenceHighlighter.Clear();
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"参照元数: {_referencePaths.Count}");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _referencePaths.Count; i++)
            {
                DrawResultRow(_referencePaths[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsUI()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("設定ファイル", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _settings = (AssetReferenceFinderSettings)EditorGUILayout.ObjectField(
                "Settings",
                _settings,
                typeof(AssetReferenceFinderSettings),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                AssetReferenceFinderSettings.SetActive(_settings);
            }

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

            if (GUILayout.Button("設定を取得"))
            {
                _settings = AssetReferenceFinderSettings.GetOrCreate();
                AssetReferenceFinderSettings.SetActive(_settings);
                Selection.activeObject = _settings;
                EditorGUIUtility.PingObject(_settings);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void FocusSettingsAsset(AssetReferenceFinderSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            AssetReferenceFinderSettings.SetActive(settings);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private void DrawResultRow(string path)
        {
            EditorGUILayout.BeginHorizontal();
            var fileName = Path.GetFileName(path);
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
            EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
        }

        private void SetTargetFromSelection()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            _targetAsset = selected;
        }

        private bool CanScanTarget()
        {
            if (_targetAsset == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(_targetAsset);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return !AssetDatabase.IsValidFolder(path);
        }

        private void ScanReferences()
        {
            if (!TryGetTargetPath(out var targetPath))
            {
                EditorUtility.DisplayDialog("Asset Reference Finder", "参照先アセットが無効です。Project 内のアセットを選択してください。", "OK");
                return;
            }

            _referencePaths.Clear();
            _isScanning = true;

            try
            {
                if (_settings == null)
                {
                    _settings = AssetReferenceFinderSettings.GetOrCreate();
                }

                var extensionFilter = ProjectScanFilterUtility.BuildExtensionFilter(_settings.ExtensionsCsv);
                var folderRoots = ProjectScanFilterUtility.BuildFolderRoots(_settings.TargetFolders);
                var targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
                var referenceGuids = new HashSet<string>();
                var guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                    {
                        continue;
                    }

                    if (path == targetPath)
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
                            "Asset Reference Finder",
                            path,
                            (float)i / guids.Length))
                    {
                        break;
                    }

                    if (ContainsReference(path, targetPath))
                    {
                        _referencePaths.Add(path);
                        referenceGuids.Add(guids[i]);
                    }
                }

                if (_referencePaths.Count > 0 && !string.IsNullOrEmpty(targetGuid))
                {
                    referenceGuids.Add(targetGuid);
                }

                ProjectAssetReferenceHighlighter.SetReferenceGuids(referenceGuids);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
            }
        }

        private bool TryGetTargetPath(out string targetPath)
        {
            targetPath = string.Empty;
            if (_targetAsset == null)
            {
                return false;
            }

            targetPath = AssetDatabase.GetAssetPath(_targetAsset);
            if (string.IsNullOrEmpty(targetPath) || AssetDatabase.IsValidFolder(targetPath))
            {
                return false;
            }

            return true;
        }

        private static bool ContainsReference(string path, string targetPath)
        {
            var dependencies = AssetDatabase.GetDependencies(path, false);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (dependencies[i] == targetPath)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
