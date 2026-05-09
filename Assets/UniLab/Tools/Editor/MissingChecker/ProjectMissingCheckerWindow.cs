using System.Collections.Generic;
using System.IO;
using System.Text;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.MissingChecker
{
    /// <summary>
    /// Editor window that scans project assets for missing references and displays
    /// field-level detail with foldout per asset.
    /// </summary>
    public class ProjectMissingCheckerWindow : EditorWindow
    {
        /// <summary>
        /// Represents a single asset entry that contains missing references.
        /// </summary>
        private struct MissingAssetEntry
        {
            public string Path;
            public List<MissingFieldInfo> Fields;
        }

        private readonly List<MissingAssetEntry> _missingEntries = new();
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private Vector2 _scrollPosition;
        private bool _isScanning;
        private ProjectMissingCheckerSettings _settings;
        private const string SettingsAssetPath = "Assets/Generated/UniCore/MissingCheckerSettings.asset";

        /// <summary>
        /// Opens the Missing Checker editor window.
        /// </summary>
        [MenuItem("UniLab/Tools/Missing Checker/Open Window")]
        public static void ShowWindow()
        {
            GetWindow<ProjectMissingCheckerWindow>("Missing Checker");
        }

        /// <summary>
        /// Selects the Missing Checker settings asset in the Inspector.
        /// </summary>
        [MenuItem("UniLab/Tools/Missing Checker/Settings")]
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
            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.MissingCheckerTitle), EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            DrawSettingsUI();

            using (new EditorGUI.DisabledScope(_isScanning))
            {
                if (_settings == null)
                {
                    EditorGUILayout.HelpBox(EditorToolLabels.Get(LabelKey.MissingSettingsNotFound), MessageType.Warning);
                }

                if (GUILayout.Button(EditorToolLabels.Get(LabelKey.ScanAllAssets)))
                {
                    if (_settings != null)
                    {
                        ScanAllAssets();
                    }
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Missing 検出数: {_missingEntries.Count}");

            if (_missingEntries.Count > 0)
            {
                if (GUILayout.Button(EditorToolLabels.Get(LabelKey.CsvExport)))
                {
                    ExportCsv();
                }
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < _missingEntries.Count; i++)
            {
                DrawMissingAssetEntry(_missingEntries[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawMissingAssetEntry(MissingAssetEntry entry)
        {
            if (!_foldoutStates.TryGetValue(entry.Path, out bool isExpanded))
            {
                isExpanded = false;
            }

            var fieldCount = entry.Fields != null ? entry.Fields.Count : 0;

            EditorGUILayout.BeginHorizontal();

            isExpanded = EditorGUILayout.Foldout(isExpanded, $"({fieldCount} fields)", true);
            _foldoutStates[entry.Path] = isExpanded;

            ProjectScanEditorUtility.DrawAssetRow(entry.Path);

            EditorGUILayout.EndHorizontal();

            if (isExpanded && entry.Fields != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(entry.Path, EditorStyles.miniLabel);
                for (int fieldIndex = 0; fieldIndex < entry.Fields.Count; fieldIndex++)
                {
                    var field = entry.Fields[fieldIndex];
                    var fieldLabel = string.IsNullOrEmpty(field.PropertyPath)
                        ? field.ComponentTypeName
                        : $"{field.ComponentTypeName}.{field.PropertyPath}";
                    EditorGUILayout.LabelField(fieldLabel, EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawSettingsUI()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.Settings), EditorStyles.boldLabel);

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
                var next = EditorGUILayout.ToggleLeft(EditorToolLabels.Get(LabelKey.HierarchyHighlightToggle), _settings.EnableHierarchyHighlight);
                if (next != _settings.EnableHierarchyHighlight)
                {
                    _settings.EnableHierarchyHighlight = next;
                    _settings.SaveAsset();
                }
            }

            var dropRect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, EditorToolLabels.Get(LabelKey.DropSettingsHere));
            HandleSettingsDrop(dropRect);

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

            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.CreateSettings)))
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
            var existing = AssetDatabase.LoadAssetAtPath<ProjectMissingCheckerSettings>(SettingsAssetPath);
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

            ProjectScanEditorUtility.EnsureFolderExists(targetFolder);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(targetFolder, "MissingCheckerSettings.asset"));
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private void ScanAllAssets()
        {
            _missingEntries.Clear();
            _foldoutStates.Clear();
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

                    var fields = CollectMissingFieldsAtPath(path);
                    if (fields.Count > 0)
                    {
                        _missingEntries.Add(new MissingAssetEntry
                        {
                            Path = path,
                            Fields = fields
                        });
                        missingSelfGuids.Add(guids[i]);
                        ProjectScanEditorUtility.CollectParentFolderGuids(path, missingParentGuids);
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

        private static List<MissingFieldInfo> CollectMissingFieldsAtPath(string path)
        {
            if (path.EndsWith(".unity"))
            {
                return CollectMissingFieldsInScene(path);
            }

            var allFields = new List<MissingFieldInfo>();
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                if (asset == null)
                {
                    allFields.Add(new MissingFieldInfo("(Null Sub-Asset)", ""));
                    continue;
                }

                if (asset is GameObject prefabRoot)
                {
                    allFields.AddRange(MissingReferenceUtility.CollectMissingFields(prefabRoot));
                }
                else
                {
                    allFields.AddRange(MissingReferenceUtility.CollectMissingFields(asset));
                }
            }

            return allFields;
        }

        private static List<MissingFieldInfo> CollectMissingFieldsInScene(string path)
        {
            return SceneScanUtility.ProcessScene(path, scene =>
            {
                var allFields = new List<MissingFieldInfo>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    allFields.AddRange(MissingReferenceUtility.CollectMissingFields(root));
                }

                return allFields;
            });
        }

        private void ExportCsv()
        {
            var savePath = CsvExportUtility.ShowSavePanel("Export Missing References CSV", "missing-references.csv");
            if (savePath == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Path,ComponentType,PropertyPath");
            for (int entryIndex = 0; entryIndex < _missingEntries.Count; entryIndex++)
            {
                var entry = _missingEntries[entryIndex];
                if (entry.Fields == null || entry.Fields.Count == 0)
                {
                    continue;
                }

                for (int fieldIndex = 0; fieldIndex < entry.Fields.Count; fieldIndex++)
                {
                    var field = entry.Fields[fieldIndex];
                    builder.Append(ProjectScanEditorUtility.EscapeCsv(entry.Path));
                    builder.Append(',');
                    builder.Append(ProjectScanEditorUtility.EscapeCsv(field.ComponentTypeName));
                    builder.Append(',');
                    builder.Append(ProjectScanEditorUtility.EscapeCsv(field.PropertyPath));
                    builder.AppendLine();
                }
            }

            CsvExportUtility.WriteAndLog(savePath, builder);
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
    }
}
