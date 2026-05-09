using System.Collections.Generic;
using System.Text;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.ComponentUsageProfiler
{
    /// <summary>
    /// Editor window that checks whether MonoBehaviour scripts in the specified folders
    /// are placed in any scene or prefab. Results are shown in "使用中" / "未使用" tabs.
    /// Note: AddComponent による動的追加は検出対象外です。
    /// </summary>
    public class ComponentUsageProfilerWindow : EditorWindow
    {
        private const int MaxVisibleLocations = 100;

        private List<ScriptUsageEntry> _allEntries = new();
        private List<ScriptUsageEntry> _usedEntries = new();
        private List<ScriptUsageEntry> _unusedEntries = new();
        private Vector2 _scrollPosition;
        private bool _isScanning;
        private string _searchText = "";
        private bool _isFilterDirty = true;
        private Dictionary<string, bool> _foldoutStates = new();

        private int _selectedTab = 1;
        // Why: cached in EditorToolLabels, rebuilt on language switch only
        private static string[] TabLabels => EditorToolLabels.TabLabels;

        private List<DefaultAsset> _targetFolders = new();

        private string _cachedSearchText = "";

        /// <summary>
        /// Opens the Component Usage Profiler window.
        /// </summary>
        [MenuItem("UniLab/Tools/Script Usage Checker/Open Window")]
        public static void ShowWindow()
        {
            GetWindow<ComponentUsageProfilerWindow>("Script Usage Checker");
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTargetFolderSection();
            DrawSearchFilter();
            DrawResultSummary();
            DrawExportButton();
            RebuildFilteredEntriesIfNeeded();
            DrawTabToolbar();
            DrawResults();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.ScriptUsageCheckerTitle), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(EditorToolLabels.Get(LabelKey.ScriptUsageDescription), MessageType.Info);
            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(_isScanning || _targetFolders.Count == 0))
            {
                if (GUILayout.Button(EditorToolLabels.Get(LabelKey.ScanExecute)))
                {
                    ExecuteScan();
                }
            }

            if (_targetFolders.Count == 0)
            {
                EditorGUILayout.HelpBox(EditorToolLabels.Get(LabelKey.CheckTargetFolderHint), MessageType.Warning);
            }

            EditorGUILayout.Space(4);
        }

        private void DrawTargetFolderSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(EditorToolLabels.Get(LabelKey.CheckTargetFolder), EditorStyles.boldLabel);

            for (int i = _targetFolders.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                _targetFolders[i] = (DefaultAsset)EditorGUILayout.ObjectField(
                    _targetFolders[i], typeof(DefaultAsset), false);

                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    _targetFolders.RemoveAt(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.FolderAdd)))
            {
                _targetFolders.Add(null);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawSearchFilter()
        {
            if (_allEntries.Count == 0)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            _searchText = EditorGUILayout.TextField(EditorToolLabels.Get(LabelKey.Search), _searchText);
            if (EditorGUI.EndChangeCheck())
            {
                _isFilterDirty = true;
            }
        }

        private void DrawResultSummary()
        {
            if (_allEntries.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"チェック対象: {_allEntries.Count}  使用中: {_usedEntries.Count}  未使用: {_unusedEntries.Count}");
        }

        private void DrawExportButton()
        {
            if (_allEntries.Count == 0)
            {
                return;
            }

            if (GUILayout.Button(EditorToolLabels.Get(LabelKey.CsvExport)))
            {
                ExportCsv();
            }

            EditorGUILayout.Space(4);
        }

        private void DrawTabToolbar()
        {
            if (_allEntries.Count == 0)
            {
                return;
            }

            _selectedTab = GUILayout.Toolbar(_selectedTab, TabLabels);
            EditorGUILayout.Space(4);
        }

        private void DrawResults()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_selectedTab == 0)
            {
                DrawScriptList(_usedEntries, true);
            }
            else
            {
                DrawScriptList(_unusedEntries, false);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawScriptList(List<ScriptUsageEntry> entries, bool showLocations)
        {
            if (entries.Count == 0)
            {
                var message = _selectedTab == 0
                    ? EditorToolLabels.Get(LabelKey.NoUsedScripts)
                    : EditorToolLabels.Get(LabelKey.NoUnusedScripts);
                EditorGUILayout.LabelField(message);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (showLocations && entry.Locations.Count > 0)
                {
                    DrawEntryWithFoldout(entry);
                }
                else
                {
                    DrawEntryRow(entry);
                }
            }
        }

        private void DrawEntryWithFoldout(ScriptUsageEntry entry)
        {
            if (!_foldoutStates.TryGetValue(entry.ScriptPath, out var isExpanded))
            {
                isExpanded = false;
            }

            var label = entry.TypeName + "  (" + entry.Locations.Count + " 箇所)";
            var newExpanded = EditorGUILayout.Foldout(isExpanded, label, true);
            _foldoutStates[entry.ScriptPath] = newExpanded;

            // Script path as mini label
            EditorGUILayout.LabelField(entry.ScriptPath, EditorStyles.miniLabel);

            if (!newExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            var visibleCount = entry.Locations.Count > MaxVisibleLocations ? MaxVisibleLocations : entry.Locations.Count;
            for (int i = 0; i < visibleCount; i++)
            {
                var location = entry.Locations[i];
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(location.AssetPath, EditorStyles.miniButton, GUILayout.MaxWidth(300)))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(location.AssetPath);
                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }

                EditorGUILayout.LabelField(location.GameObjectPath, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            if (entry.Locations.Count > MaxVisibleLocations)
            {
                EditorGUILayout.LabelField($"... 他 {entry.Locations.Count - MaxVisibleLocations} 件", EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawEntryRow(ScriptUsageEntry entry)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(entry.TypeName, EditorStyles.miniButton))
            {
                var scriptAsset = AssetDatabase.LoadMainAssetAtPath(entry.ScriptPath);
                if (scriptAsset != null)
                {
                    AssetDatabase.OpenAsset(scriptAsset);
                }
            }

            EditorGUILayout.LabelField(entry.ScriptPath, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void ExecuteScan()
        {
            _isScanning = true;

            try
            {
                var folderRoots = ProjectScanFilterUtility.BuildFolderRoots(_targetFolders);
                _allEntries = ComponentUsageScanner.CheckScriptUsage(folderRoots);
                _isFilterDirty = true;
                _foldoutStates.Clear();
            }
            finally
            {
                _isScanning = false;
            }
        }

        private void RebuildFilteredEntriesIfNeeded()
        {
            if (!_isFilterDirty && _cachedSearchText == _searchText)
            {
                return;
            }

            _usedEntries.Clear();
            _unusedEntries.Clear();

            for (int i = 0; i < _allEntries.Count; i++)
            {
                var entry = _allEntries[i];

                if (!string.IsNullOrEmpty(_searchText)
                    && entry.TypeName.IndexOf(_searchText, System.StringComparison.OrdinalIgnoreCase) < 0
                    && entry.ScriptPath.IndexOf(_searchText, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (entry.IsUsed)
                {
                    _usedEntries.Add(entry);
                }
                else
                {
                    _unusedEntries.Add(entry);
                }
            }

            _cachedSearchText = _searchText;
            _isFilterDirty = false;
        }

        private void ExportCsv()
        {
            var savePath = CsvExportUtility.ShowSavePanel("Export Script Usage CSV", "script-usage.csv");
            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("ScriptPath,TypeName,TypeFullName,IsUsed,LocationCount");

            for (int i = 0; i < _allEntries.Count; i++)
            {
                var entry = _allEntries[i];
                builder.Append(ProjectScanEditorUtility.EscapeCsv(entry.ScriptPath));
                builder.Append(',');
                builder.Append(ProjectScanEditorUtility.EscapeCsv(entry.TypeName));
                builder.Append(',');
                builder.Append(ProjectScanEditorUtility.EscapeCsv(entry.TypeFullName));
                builder.Append(',');
                builder.Append(entry.IsUsed ? "true" : "false");
                builder.Append(',');
                builder.Append(entry.Locations.Count);
                builder.AppendLine();
            }

            CsvExportUtility.WriteAndLog(savePath, builder);
        }
    }
}
