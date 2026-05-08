using System.Collections.Generic;
using System.IO;
using System.Text;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.UnreferencedAssetFinder
{
    /// <summary>
    /// Provides an editor window that scans project assets for GUIDs with no project dependencies.
    /// </summary>
    public class UnreferencedAssetFinderWindow : EditorWindow
    {
        private readonly List<UnreferencedAssetEntry> _unreferencedEntries = new();
        private readonly List<UnreferencedAssetEntry> _sortedFilteredEntries = new();
        private Vector2 _scroll;
        private bool _isScanning;
        private string _filterExtension = "";
        private int _sortMode;
        private bool _isResultCacheDirty = true;
        private string _cachedFilterExtension = "";
        private int _cachedSortMode = -1;
        private UnreferencedAssetFinderSettings _settings;

        /// <summary>
        /// Represents a single unreferenced asset entry with its metadata.
        /// </summary>
        private struct UnreferencedAssetEntry
        {
            public string Path;
            public string Guid;
            public long FileSize;
        }

        /// <summary>
        /// Opens the unreferenced asset finder window.
        /// </summary>
        [MenuItem("UniLab/Tools/Unreferenced Asset Finder/Open Window")]
        public static void ShowWindow()
        {
            GetWindow<UnreferencedAssetFinderWindow>("Unreferenced Assets");
        }

        /// <summary>
        /// Selects the unreferenced asset finder settings asset.
        /// </summary>
        [MenuItem("UniLab/Tools/Unreferenced Asset Finder/Settings")]
        public static void ShowSettings()
        {
            var settings = UnreferencedAssetFinderSettings.GetOrCreate();
            FocusSettingsAsset(settings);
        }

        private void OnEnable()
        {
            _settings = UnreferencedAssetFinderSettings.GetOrCreate();
            UnreferencedAssetFinderSettings.SetActive(_settings);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("未参照アセットを検索", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            DrawSettingsUI();

            using (new EditorGUI.DisabledScope(_isScanning))
            {
                if (GUILayout.Button("全アセットをスキャン"))
                {
                    ScanAllAssets();
                }

                if (GUILayout.Button("ハイライト解除"))
                {
                    ClearResults();
                    ProjectUnreferencedAssetHighlighter.Clear();
                }
            }

            EditorGUILayout.Space(6);
            DrawResultHeader();

            if (_unreferencedEntries.Count > 0)
            {
                DrawResultControls();
                DrawResultActionButtons();
            }

            RebuildSortedFilteredEntriesIfNeeded();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _sortedFilteredEntries.Count; i++)
            {
                DrawResultRow(_sortedFilteredEntries[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsUI()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("設定ファイル", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _settings = (UnreferencedAssetFinderSettings)EditorGUILayout.ObjectField(
                "Settings",
                _settings,
                typeof(UnreferencedAssetFinderSettings),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                UnreferencedAssetFinderSettings.SetActive(_settings);
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
                _settings = UnreferencedAssetFinderSettings.GetOrCreate();
                UnreferencedAssetFinderSettings.SetActive(_settings);
                Selection.activeObject = _settings;
                EditorGUIUtility.PingObject(_settings);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void FocusSettingsAsset(UnreferencedAssetFinderSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            UnreferencedAssetFinderSettings.SetActive(settings);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private void ScanAllAssets()
        {
            ClearResults();
            _isScanning = true;

            try
            {
                if (_settings == null)
                {
                    _settings = UnreferencedAssetFinderSettings.GetOrCreate();
                }

                var extensionFilter = ProjectScanFilterUtility.BuildExtensionFilter(_settings.ExtensionsCsv);
                var folderRoots = ProjectScanFilterUtility.BuildFolderRoots(_settings.TargetFolders);
                var candidateGuids = new List<string>();
                var candidateGuidSet = new HashSet<string>();
                var referencedGuids = new HashSet<string>();
                var parentFolderGuids = new HashSet<string>();
                var guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });

                CollectCandidateGuids(guids, extensionFilter, folderRoots, candidateGuids, candidateGuidSet);
                CollectReferencedGuids(guids, referencedGuids);
                AddExcludedGuids(candidateGuidSet, referencedGuids);

                var unreferencedGuids = new List<string>();
                for (int i = 0; i < candidateGuids.Count; i++)
                {
                    var guid = candidateGuids[i];
                    if (referencedGuids.Contains(guid))
                    {
                        continue;
                    }

                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    _unreferencedEntries.Add(new UnreferencedAssetEntry
                    {
                        Path = path,
                        Guid = guid,
                        FileSize = GetFileSize(path)
                    });
                    unreferencedGuids.Add(guid);
                    ProjectScanEditorUtility.CollectParentFolderGuids(path, parentFolderGuids);
                }

                _isResultCacheDirty = true;
                ProjectUnreferencedAssetHighlighter.SetUnreferencedGuids(unreferencedGuids, parentFolderGuids);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
            }
        }

        private static void CollectCandidateGuids(
            IReadOnlyList<string> guids,
            HashSet<string> extensionFilter,
            List<string> folderRoots,
            List<string> candidateGuids,
            HashSet<string> candidateGuidSet)
        {
            for (int i = 0; i < guids.Count; i++)
            {
                var guid = guids[i];
                var path = AssetDatabase.GUIDToAssetPath(guid);
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

                candidateGuids.Add(guid);
                candidateGuidSet.Add(guid);
            }
        }

        private static void CollectReferencedGuids(IReadOnlyList<string> guids, HashSet<string> referencedGuids)
        {
            for (int i = 0; i < guids.Count; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Unreferenced Asset Finder",
                        path,
                        (float)i / guids.Count))
                {
                    break;
                }

                var dependencies = AssetDatabase.GetDependencies(path, false);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    var dependencyPath = dependencies[dependencyIndex];
                    if (dependencyPath == path)
                    {
                        continue;
                    }

                    var dependencyGuid = AssetDatabase.AssetPathToGUID(dependencyPath);
                    if (!string.IsNullOrEmpty(dependencyGuid))
                    {
                        referencedGuids.Add(dependencyGuid);
                    }
                }
            }
        }

        private void AddExcludedGuids(HashSet<string> candidateGuidSet, HashSet<string> referencedGuids)
        {
            foreach (var guid in candidateGuidSet)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (ShouldExcludeAsReferenced(path))
                {
                    referencedGuids.Add(guid);
                }
            }
        }

        private bool ShouldExcludeAsReferenced(string path)
        {
            if (_settings.ExcludeResourcesFolder && IsUnderFolderNamed(path, "Resources"))
            {
                return true;
            }

            if (_settings.ExcludeStreamingAssetsFolder && IsUnderFolderNamed(path, "StreamingAssets"))
            {
                return true;
            }

            if (_settings.ExcludeBuildScenes && IsEnabledBuildScene(path))
            {
                return true;
            }


            return false;
        }

        private static bool IsUnderFolderNamed(string path, string folderName)
        {
            var marker = "/" + folderName + "/";
            return path.Contains(marker);
        }

        private static bool IsEnabledBuildScene(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                var scene = scenes[i];
                if (scene.enabled && scene.path == path)
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawResultHeader()
        {
            var totalSize = CalculateTotalFileSize(_unreferencedEntries);
            EditorGUILayout.LabelField($"未参照アセット数: {_unreferencedEntries.Count}  合計サイズ: {FormatFileSize(totalSize)}");
        }

        private void DrawResultControls()
        {
            EditorGUI.BeginChangeCheck();
            _filterExtension = EditorGUILayout.TextField("拡張子フィルタ", _filterExtension);
            _sortMode = EditorGUILayout.Popup("ソート", _sortMode, new[] { "パス順", "サイズ大", "拡張子" });

            if (EditorGUI.EndChangeCheck())
            {
                _isResultCacheDirty = true;
            }
        }

        private void DrawResultActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("CSV エクスポート"))
            {
                ExportCsv();
            }

            if (GUILayout.Button("未参照アセットを隔離"))
            {
                MoveUnreferencedAssetsToIsolationFolder();
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawResultRow(UnreferencedAssetEntry entry)
        {
            EditorGUILayout.BeginHorizontal();
            var fileName = Path.GetFileName(entry.Path);
            if (GUILayout.Button(fileName, EditorStyles.miniButtonLeft))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(entry.Path);
                if (obj != null)
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
            }

            if (GUILayout.Button("開く", EditorStyles.miniButtonRight, GUILayout.Width(50)))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(entry.Path);
                if (obj != null)
                {
                    AssetDatabase.OpenAsset(obj);
                }
            }

            EditorGUILayout.LabelField(FormatFileSize(entry.FileSize), EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(entry.Path, EditorStyles.miniLabel);
        }

        private void RebuildSortedFilteredEntriesIfNeeded()
        {
            if (!_isResultCacheDirty && _cachedFilterExtension == _filterExtension && _cachedSortMode == _sortMode)
            {
                return;
            }

            _sortedFilteredEntries.Clear();
            var normalizedExtension = NormalizeExtension(_filterExtension);
            for (int i = 0; i < _unreferencedEntries.Count; i++)
            {
                var entry = _unreferencedEntries[i];
                if (!PassExtensionFilter(entry.Path, normalizedExtension))
                {
                    continue;
                }

                _sortedFilteredEntries.Add(entry);
            }

            SortEntries(_sortedFilteredEntries, _sortMode);
            _cachedFilterExtension = _filterExtension;
            _cachedSortMode = _sortMode;
            _isResultCacheDirty = false;
        }

        private static bool PassExtensionFilter(string path, string normalizedExtension)
        {
            if (string.IsNullOrEmpty(normalizedExtension))
            {
                return true;
            }

            var extension = NormalizeExtension(Path.GetExtension(path));
            return extension == normalizedExtension;
        }

        private static void SortEntries(List<UnreferencedAssetEntry> entries, int sortMode)
        {
            if (sortMode == 1)
            {
                entries.Sort((left, right) =>
                {
                    var sizeComparison = right.FileSize.CompareTo(left.FileSize);
                    return sizeComparison != 0
                        ? sizeComparison
                        : string.CompareOrdinal(left.Path, right.Path);
                });
                return;
            }

            if (sortMode == 2)
            {
                entries.Sort((left, right) =>
                {
                    var extensionComparison = string.CompareOrdinal(
                        NormalizeExtension(Path.GetExtension(left.Path)),
                        NormalizeExtension(Path.GetExtension(right.Path)));
                    return extensionComparison != 0
                        ? extensionComparison
                        : string.CompareOrdinal(left.Path, right.Path);
                });
                return;
            }

            entries.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
        }

        private static string NormalizeExtension(string extension)
        {
            return extension.Trim().TrimStart('.').ToLowerInvariant();
        }

        private static long GetFileSize(string path)
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                return 0L;
            }

            return fileInfo.Length;
        }

        private static long CalculateTotalFileSize(IReadOnlyList<UnreferencedAssetEntry> entries)
        {
            var totalSize = 0L;
            for (int i = 0; i < entries.Count; i++)
            {
                totalSize += entries[i].FileSize;
            }

            return totalSize;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024L)
            {
                return bytes + " bytes";
            }

            var units = new[] { "KB", "MB", "GB", "TB" };
            var size = bytes / 1024d;
            var unitIndex = 0;
            while (size >= 1024d && unitIndex < units.Length - 1)
            {
                size /= 1024d;
                unitIndex++;
            }

            return size.ToString("0.#") + " " + units[unitIndex];
        }

        private void ExportCsv()
        {
            var savePath = EditorUtility.SaveFilePanel(
                "Export Unreferenced Assets CSV",
                "",
                "unreferenced-assets.csv",
                "csv");
            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Path,GUID,Extension,FileSize");
            for (int i = 0; i < _unreferencedEntries.Count; i++)
            {
                var entry = _unreferencedEntries[i];
                builder.Append(EscapeCsv(entry.Path));
                builder.Append(',');
                builder.Append(EscapeCsv(entry.Guid));
                builder.Append(',');
                builder.Append(EscapeCsv(NormalizeExtension(Path.GetExtension(entry.Path))));
                builder.Append(',');
                builder.Append(entry.FileSize);
                builder.AppendLine();
            }

            File.WriteAllText(savePath, builder.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static string EscapeCsv(string value)
        {
            if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n") && !value.Contains("\r"))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void MoveUnreferencedAssetsToIsolationFolder()
        {
            var isolationFolder = "Assets/_Unused/" + System.DateTime.Now.ToString("yyyy-MM-dd");
            if (!EditorUtility.DisplayDialog(
                    "未参照アセットを隔離",
                    _unreferencedEntries.Count + " 件のアセットを " + isolationFolder + "/ に移動します",
                    "移動",
                    "キャンセル"))
            {
                return;
            }

            ProjectScanEditorUtility.EnsureFolderExists(isolationFolder);
            MoveEntriesToFolder(isolationFolder);
            ClearResults();
            ProjectUnreferencedAssetHighlighter.Clear();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PingAssetFolder(isolationFolder);
        }

        private void MoveEntriesToFolder(string isolationFolder)
        {
            for (int i = 0; i < _unreferencedEntries.Count; i++)
            {
                var entry = _unreferencedEntries[i];
                var destinationPath = AssetDatabase.GenerateUniqueAssetPath(
                    isolationFolder + "/" + Path.GetFileName(entry.Path));
                var error = AssetDatabase.MoveAsset(entry.Path, destinationPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning("Failed to move unreferenced asset: " + entry.Path + " -> " + error);
                }
            }
        }

        private void ClearResults()
        {
            _unreferencedEntries.Clear();
            _sortedFilteredEntries.Clear();
            _isResultCacheDirty = true;
        }

        private static void PingAssetFolder(string folderPath)
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            if (folder == null)
            {
                return;
            }

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
    }
}
