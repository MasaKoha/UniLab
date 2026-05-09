using System.Collections.Generic;
using System.IO;
using System.Text;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniLab.Tools.Editor.AssetReferenceFinder
{
    /// <summary>
    /// Editor window that finds which assets reference the specified target asset(s).
    /// Supports multiple target assets, reverse-reference tree expansion, and CSV export.
    /// </summary>
    public class ProjectAssetReferenceFinderWindow : EditorWindow
    {
        private const int MaxTargetAssets = 20;
        private const int MaxReverseTreeDepth = 2;
        private const float DropAreaHeight = 50f;
        private const float IndentWidth = 20f;

        private readonly List<Object> _targetAssets = new();
        private readonly Dictionary<string, List<string>> _referencePathsByTarget = new();
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private readonly Dictionary<string, List<string>> _reverseReferenceCache = new();
        private readonly List<string> _orderedTargetPaths = new();
        private Vector2 _scrollPosition;
        private bool _isScanning;
        private int _totalReferenceCount;
        private AssetReferenceFinderSettings _settings;

        /// <summary>
        /// Opens the Asset Reference Finder window.
        /// </summary>
        [MenuItem("UniLab/Tools/Asset Reference Finder/Open Window")]
        public static void ShowWindow()
        {
            GetWindow<ProjectAssetReferenceFinderWindow>("Asset Ref Finder");
        }

        /// <summary>
        /// Opens the settings asset in the Inspector.
        /// </summary>
        [MenuItem("UniLab/Tools/Asset Reference Finder/Settings")]
        public static void ShowSettings()
        {
            var settings = AssetReferenceFinderSettings.GetOrCreate();
            FocusSettingsAsset(settings);
        }

        /// <summary>
        /// Context menu entry: find references for the currently selected asset(s).
        /// </summary>
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
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                var path = AssetDatabase.GetAssetPath(selectedObjects[i]);
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnEnable()
        {
            _settings = AssetReferenceFinderSettings.GetOrCreate();
            AssetReferenceFinderSettings.SetActive(_settings);
            SetTargetFromSelection();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.AssetReferenceFinderTitle), EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            DrawSettingsUI();
            EditorGUILayout.Space(4);

            DrawTargetAssetsUI();

            if (!CanScanTarget())
            {
                EditorGUILayout.HelpBox("Project 内のアセットを 1 つ以上指定してください。", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(_isScanning || !CanScanTarget()))
            {
                if (GUILayout.Button(EditorToolLabels.Get(LabelKey.SearchReferences)))
                {
                    ScanReferences();
                }
            }

            using (new EditorGUI.DisabledScope(_isScanning))
            {
                if (GUILayout.Button(EditorToolLabels.Get(LabelKey.ClearHighlight)))
                {
                    ClearResults();
                    ProjectAssetReferenceHighlighter.Clear();
                }
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField($"参照元数: {_totalReferenceCount}");

            // --- CSV export ---
            if (_totalReferenceCount > 0)
            {
                if (GUILayout.Button(EditorToolLabels.Get(LabelKey.CsvExport)))
                {
                    ExportCsv();
                }
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawGroupedResults();
            EditorGUILayout.EndScrollView();
        }

        // --- Target assets UI (drag-and-drop area + list) ---

        private void DrawTargetAssetsUI()
        {
            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.TargetAssets), EditorStyles.boldLabel);

            DrawDropArea();

            for (int i = 0; i < _targetAssets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var asset = _targetAssets[i];
                var assetPath = asset != null ? AssetDatabase.GetAssetPath(asset) : "(null)";
                EditorGUILayout.LabelField(Path.GetFileName(assetPath), EditorStyles.miniLabel);

                if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    _targetAssets.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (_targetAssets.Count > 0)
            {
                if (GUILayout.Button(EditorToolLabels.Get(LabelKey.Clear), GUILayout.Width(60)))
                {
                    _targetAssets.Clear();
                }
            }

            EditorGUILayout.Space(4);
        }

        private void DrawDropArea()
        {
            var dropArea = GUILayoutUtility.GetRect(0f, DropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, EditorToolLabels.Get(LabelKey.TargetAssetsHint));

            var currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var draggedObject in DragAndDrop.objectReferences)
                {
                    AddTargetAsset(draggedObject);
                }

                currentEvent.Use();
            }
        }

        private void AddTargetAsset(Object asset)
        {
            if (asset == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            if (_targetAssets.Count >= MaxTargetAssets)
            {
                Debug.LogWarning($"[AssetReferenceFinder] Target asset limit reached ({MaxTargetAssets}).");
                return;
            }

            // Avoid duplicates.
            for (int i = 0; i < _targetAssets.Count; i++)
            {
                if (_targetAssets[i] == asset)
                {
                    return;
                }
            }

            _targetAssets.Add(asset);
        }

        // --- Settings UI ---

        private void DrawSettingsUI()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.Settings), EditorStyles.boldLabel);

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
            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.OpenSettings)))
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

            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.GetSettings)))
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

        // --- Grouped result display with reverse-reference tree ---

        private void DrawGroupedResults()
        {
            for (int targetIndex = 0; targetIndex < _orderedTargetPaths.Count; targetIndex++)
            {
                var targetPath = _orderedTargetPaths[targetIndex];
                if (!_referencePathsByTarget.TryGetValue(targetPath, out var referencePaths))
                {
                    continue;
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"[{Path.GetFileName(targetPath)}]  ({referencePaths.Count} 件)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(targetPath, EditorStyles.miniLabel);

                for (int i = 0; i < referencePaths.Count; i++)
                {
                    DrawResultRowWithFoldout(referencePaths[i], 0);
                }
            }
        }

        private void DrawResultRowWithFoldout(string path, int depth)
        {
            float indent = depth * IndentWidth;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent);

            // Foldout toggle only up to max depth.
            bool canExpand = depth < MaxReverseTreeDepth;
            if (canExpand)
            {
                if (!_foldoutStates.TryGetValue(path, out bool expanded))
                {
                    expanded = false;
                    _foldoutStates[path] = false;
                }

                bool nextExpanded = EditorGUILayout.Foldout(expanded, "", true);
                if (nextExpanded != expanded)
                {
                    _foldoutStates[path] = nextExpanded;
                    // Trigger reverse lookup on first expand.
                    if (nextExpanded && !_reverseReferenceCache.ContainsKey(path))
                    {
                        _reverseReferenceCache[path] = FindReverseReferences(path);
                    }
                }
            }

            var fileName = Path.GetFileName(path);
            if (GUILayout.Button(fileName, EditorStyles.miniButtonLeft))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }

            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.Open), EditorStyles.miniButtonRight, GUILayout.Width(50)))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Path label with indent.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent + 18f);
            EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Recursive children if expanded.
            if (canExpand && _foldoutStates.TryGetValue(path, out bool isExpanded) && isExpanded)
            {
                if (_reverseReferenceCache.TryGetValue(path, out List<string> children))
                {
                    if (children.Count == 0)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(indent + IndentWidth);
                        EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.NoReverseReference), EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        for (int i = 0; i < children.Count; i++)
                        {
                            DrawResultRowWithFoldout(children[i], depth + 1);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds all assets that reference the given asset path (reverse lookup).
        /// Uses the full project scan with the current settings filters.
        /// </summary>
        private List<string> FindReverseReferences(string targetPath)
        {
            var result = new List<string>();
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    var candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(candidatePath) || AssetDatabase.IsValidFolder(candidatePath))
                    {
                        continue;
                    }

                    if (candidatePath == targetPath)
                    {
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Reverse Reference Lookup",
                            candidatePath,
                            (float)i / guids.Length))
                    {
                        break;
                    }

                    if (ContainsReference(candidatePath, targetPath))
                    {
                        result.Add(candidatePath);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return result;
        }

        // --- CSV export ---

        private void ExportCsv()
        {
            var filePath = CsvExportUtility.ShowSavePanel(EditorToolLabels.Get(LabelKey.CsvExport), "asset_references.csv");
            if (filePath == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("TargetAsset,ReferencingAsset,Extension");

            for (int targetIndex = 0; targetIndex < _orderedTargetPaths.Count; targetIndex++)
            {
                var targetPath = _orderedTargetPaths[targetIndex];
                if (!_referencePathsByTarget.TryGetValue(targetPath, out var referencePaths))
                {
                    continue;
                }

                for (int refIndex = 0; refIndex < referencePaths.Count; refIndex++)
                {
                    var referencePath = referencePaths[refIndex];
                    var extension = Path.GetExtension(referencePath);
                    builder.AppendLine(
                        $"{ProjectScanEditorUtility.EscapeCsv(targetPath)},{ProjectScanEditorUtility.EscapeCsv(referencePath)},{ProjectScanEditorUtility.EscapeCsv(extension)}");
                }
            }

            CsvExportUtility.WriteAndLog(filePath, builder);
        }

        // --- Selection handling ---

        private void SetTargetFromSelection()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                return;
            }

            _targetAssets.Clear();
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                AddTargetAsset(selectedObjects[i]);
            }
        }

        private bool CanScanTarget()
        {
            return _targetAssets.Count > 0;
        }

        // --- Scanning ---

        private void ScanReferences()
        {
            var targetPaths = BuildTargetPaths();
            if (targetPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Asset Reference Finder",
                    EditorToolLabels.Get(LabelKey.InvalidTargetMessage),
                    "OK");
                return;
            }

            ClearResults();
            _isScanning = true;

            try
            {
                if (_settings == null)
                {
                    _settings = AssetReferenceFinderSettings.GetOrCreate();
                }

                var extensionFilter = ProjectScanFilterUtility.BuildExtensionFilter(_settings.ExtensionsCsv);
                var folderRoots = ProjectScanFilterUtility.BuildFolderRoots(_settings.TargetFolders);
                var targetPathSet = new HashSet<string>(targetPaths);
                var referenceGuids = new HashSet<string>();
                var parentFolderGuids = new HashSet<string>();

                // Initialize result buckets for each target.
                _orderedTargetPaths.Clear();
                for (int targetIndex = 0; targetIndex < targetPaths.Count; targetIndex++)
                {
                    var targetPath = targetPaths[targetIndex];
                    _referencePathsByTarget[targetPath] = new List<string>();
                    _orderedTargetPaths.Add(targetPath);
                }

                var guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });
                // perf: reuse HashSet across iterations to avoid per-asset allocation
                var dependencySet = new HashSet<string>();
                for (int i = 0; i < guids.Length; i++)
                {
                    var candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(candidatePath) || AssetDatabase.IsValidFolder(candidatePath))
                    {
                        continue;
                    }

                    if (targetPathSet.Contains(candidatePath))
                    {
                        continue;
                    }

                    if (!ProjectScanFilterUtility.PassExtensionFilter(candidatePath, extensionFilter))
                    {
                        continue;
                    }

                    if (!ProjectScanFilterUtility.PassFolderFilter(candidatePath, folderRoots))
                    {
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Asset Reference Finder",
                            candidatePath,
                            (float)i / guids.Length))
                    {
                        break;
                    }

                    var dependencies = AssetDatabase.GetDependencies(candidatePath, false);
                    dependencySet.Clear();
                    for (int d = 0; d < dependencies.Length; d++)
                    {
                        dependencySet.Add(dependencies[d]);
                    }

                    bool isReferencing = false;

                    for (int targetIndex = 0; targetIndex < targetPaths.Count; targetIndex++)
                    {
                        var targetPath = targetPaths[targetIndex];
                        if (dependencySet.Contains(targetPath))
                        {
                            _referencePathsByTarget[targetPath].Add(candidatePath);
                            isReferencing = true;
                        }
                    }

                    if (isReferencing)
                    {
                        referenceGuids.Add(guids[i]);
                        AddParentFolderGuid(candidatePath, parentFolderGuids);
                    }
                }

                parentFolderGuids.ExceptWith(referenceGuids);
                var highlightGuids = new HashSet<string>(referenceGuids);
                highlightGuids.UnionWith(parentFolderGuids);
                ProjectAssetReferenceHighlighter.SetReferenceGuids(highlightGuids, referenceGuids);

                _totalReferenceCount = 0;
                for (int targetIndex = 0; targetIndex < _orderedTargetPaths.Count; targetIndex++)
                {
                    if (_referencePathsByTarget.TryGetValue(_orderedTargetPaths[targetIndex], out var refs))
                    {
                        _totalReferenceCount += refs.Count;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
            }
        }

        private List<string> BuildTargetPaths()
        {
            var paths = new List<string>();
            for (int i = 0; i < _targetAssets.Count; i++)
            {
                if (_targetAssets[i] == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(_targetAssets[i]);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                paths.Add(path);
            }

            return paths;
        }

        private void ClearResults()
        {
            _referencePathsByTarget.Clear();
            _orderedTargetPaths.Clear();
            _foldoutStates.Clear();
            _reverseReferenceCache.Clear();
            _totalReferenceCount = 0;
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

        private static void AddParentFolderGuid(string assetPath, HashSet<string> guidSet)
        {
            if (string.IsNullOrEmpty(assetPath) || guidSet == null)
            {
                return;
            }

            var parentPath = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(parentPath))
            {
                return;
            }

            // Unity asset paths are always slash-separated.
            parentPath = parentPath.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parentPath))
            {
                return;
            }

            var parentGuid = AssetDatabase.AssetPathToGUID(parentPath);
            if (!string.IsNullOrEmpty(parentGuid))
            {
                guidSet.Add(parentGuid);
            }
        }
    }
}
