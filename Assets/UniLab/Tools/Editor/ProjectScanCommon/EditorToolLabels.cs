using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.ProjectScanCommon
{
    /// <summary>
    /// Label key enum for all UniLab Editor tool UI strings.
    /// Add new keys before Count.
    /// </summary>
    public enum LabelKey
    {
        // --- Common ---
        Open, Delete, Add, Clear, ClearAll,
        Search, Sort, Settings, OpenSettings, CreateSettings,
        GetSettings, CsvExport, Yes, No, Cancel,
        Move, DropHere, FolderAdd, SelectAll, DeselectAll,
        Reload,

        // --- Scan ---
        ScanAllAssets, ScanExecute, ClearHighlight,

        // --- Settings Editor ---
        TargetFolders, TargetFoldersHint, ExtensionsCsvLabel,

        // --- Asset Reference Finder ---
        AssetReferenceFinderTitle, TargetAssets, TargetAssetsHint,
        SearchReferences, InvalidTargetMessage, NoReverseReference,

        // --- Missing Checker ---
        MissingCheckerTitle, MissingSettingsNotFound,
        HierarchyHighlightToggle, DropSettingsHere, ReferenceColorLabel,

        // --- Unreferenced Asset Finder ---
        UnreferencedFinderTitle, ExtensionFilter, IsolateAssets, DeleteSelectedTitle,

        // --- Sort Modes ---
        SortByPath, SortBySize, SortByExtension,

        // --- Script Usage Checker ---
        ScriptUsageCheckerTitle, ScriptUsageDescription,
        CheckTargetFolder, CheckTargetFolderHint, FolderHint,
        TabUsed, TabUnused, NoUsedScripts, NoUnusedScripts,

        // --- Favorite Asset ---
        FavoriteAssetDragHint, FavoriteAssetList, TargetCategory, NewCategory,
        DefaultCategory, ConfirmClearFavorites, Confirm, NotFound,

        // --- Hierarchy Favorite ---
        HierarchyFavoriteTitle, HierarchyFavoriteDragHint,
        SceneNotSavedMessage, EntryNotFoundMessage, ParseFailedMessage,
        ConfirmClearAllBookmarks, Memo, Missing,

        // --- Open Folder ---
        UnsupportedPlatform,

        // Why: sentinel — must be last. Used for array size validation.
        Count
    }

    /// <summary>
    /// A single label entry holding key enum, Japanese text, and English text.
    /// </summary>
    public readonly struct LabelEntry
    {
        /// <summary>
        /// Enum key that identifies this label.
        /// </summary>
        public readonly LabelKey Key;

        /// <summary>
        /// Japanese localized text.
        /// </summary>
        public readonly string Ja;

        /// <summary>
        /// English localized text.
        /// </summary>
        public readonly string En;

        public LabelEntry(LabelKey key, string ja, string en)
        {
            Key = key;
            Ja = ja;
            En = en;
        }
    }

    /// <summary>
    /// Centralized UI labels used across all UniLab Editor tools.
    /// Supports JA/EN switching via UniLab/Language Setting menu.
    /// Labels are defined as LabelEntry (Key, Ja, En) for easy maintenance.
    /// Access is O(1) via array index with zero GC.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorToolLabels
    {
        private const string LanguagePrefKey = "UniLab.EditorToolLabels.Language";

        private static bool _isJapanese;

        // Why: cached arrays for Toolbar/Popup — rebuilt on language switch only
        private static string[] _cachedTabLabels;
        private static string[] _cachedSortModeLabels;

        // ======================================================================
        // Label definitions — LabelKey, Japanese, English in one place
        // ======================================================================

        private static readonly LabelEntry[] AllLabels =
        {
            // --- Common ---
            new(LabelKey.Open,           "開く",              "Open"),
            new(LabelKey.Delete,         "削除",              "Delete"),
            new(LabelKey.Add,            "追加",              "Add"),
            new(LabelKey.Clear,          "クリア",            "Clear"),
            new(LabelKey.ClearAll,       "全てクリア",         "Clear All"),
            new(LabelKey.Search,         "検索",              "Search"),
            new(LabelKey.Sort,           "ソート",             "Sort"),
            new(LabelKey.Settings,       "設定ファイル",       "Settings"),
            new(LabelKey.OpenSettings,   "設定を開く",         "Open Settings"),
            new(LabelKey.CreateSettings, "設定ファイル作成",    "Create Settings"),
            new(LabelKey.GetSettings,    "設定を取得",         "Get Settings"),
            new(LabelKey.CsvExport,      "CSV エクスポート",   "CSV Export"),
            new(LabelKey.Yes,            "はい",              "Yes"),
            new(LabelKey.No,             "いいえ",             "No"),
            new(LabelKey.Cancel,         "キャンセル",         "Cancel"),
            new(LabelKey.Move,           "移動",              "Move"),
            new(LabelKey.DropHere,       "ここにドロップ",     "Drop here"),
            new(LabelKey.FolderAdd,      "+ フォルダ追加",     "+ Add Folder"),
            new(LabelKey.SelectAll,      "全選択",             "Select All"),
            new(LabelKey.DeselectAll,    "全解除",             "Deselect All"),
            new(LabelKey.Reload,         "再読込",             "Reload"),

            // --- Scan ---
            new(LabelKey.ScanAllAssets,  "全アセットをスキャン",   "Scan All Assets"),
            new(LabelKey.ScanExecute,    "スキャン実行",          "Run Scan"),
            new(LabelKey.ClearHighlight, "ハイライト解除",        "Clear Highlight"),

            // --- Settings Editor ---
            new(LabelKey.TargetFolders,      "スキャン対象フォルダ",                          "Target Folders"),
            new(LabelKey.TargetFoldersHint,  "空欄の場合は全フォルダを対象にスキャンします。",    "Leave empty to scan all folders."),
            new(LabelKey.ExtensionsCsvLabel, "拡張子 (CSV, ドット不要)",                      "Extensions (CSV, no dot)"),

            // --- Asset Reference Finder ---
            new(LabelKey.AssetReferenceFinderTitle, "特定アセットの参照元を検索",                                        "Find Asset References"),
            new(LabelKey.TargetAssets,              "参照先アセット",                                                  "Target Assets"),
            new(LabelKey.TargetAssetsHint,          "ここにアセットをドラッグ & ドロップ",                               "Drag & drop assets here"),
            new(LabelKey.SearchReferences,          "参照元を検索",                                                    "Search References"),
            new(LabelKey.InvalidTargetMessage,      "参照先アセットが無効です。Project 内のアセットを選択してください。",     "Invalid target asset. Please select an asset from the Project."),
            new(LabelKey.NoReverseReference,        "(参照元なし)",                                                    "(No references)"),

            // --- Missing Checker ---
            new(LabelKey.MissingCheckerTitle,      "全アセットの Missing 参照をチェック",                                          "Check Missing References"),
            new(LabelKey.MissingSettingsNotFound,  "設定ファイルが見つかりません。先に設定ファイルを作成してください。",                 "Settings file not found. Please create one first."),
            new(LabelKey.HierarchyHighlightToggle, "ヒエラルキー色付けを有効",                                                    "Enable Hierarchy Highlight"),
            new(LabelKey.DropSettingsHere,          "ここに設定ファイルをドロップ",                                                 "Drop settings file here"),
            new(LabelKey.ReferenceColorLabel,       "参照アセット",                                                               "Reference Asset"),

            // --- Unreferenced Asset Finder ---
            new(LabelKey.UnreferencedFinderTitle, "未参照アセットを検索",     "Find Unreferenced Assets"),
            new(LabelKey.ExtensionFilter,         "拡張子フィルタ",          "Extension Filter"),
            new(LabelKey.IsolateAssets,           "未参照アセットを隔離",     "Isolate Unreferenced Assets"),
            new(LabelKey.DeleteSelectedTitle,     "選択アセットを削除",       "Delete Selected Assets"),

            // --- Sort Modes ---
            new(LabelKey.SortByPath,      "パス順",    "By Path"),
            new(LabelKey.SortBySize,      "サイズ大",   "By Size"),
            new(LabelKey.SortByExtension, "拡張子",     "By Extension"),

            // --- Script Usage Checker ---
            new(LabelKey.ScriptUsageCheckerTitle, "MonoBehaviour 使用状況チェッカー", "MonoBehaviour Usage Checker"),
            new(LabelKey.ScriptUsageDescription,
                "指定フォルダ内の MonoBehaviour が、いずれかのシーンまたは Prefab に配置されているかチェックします。\n" +
                "※ AddComponent<T>() や GetComponent<T>() 等によるコードからの動的利用は検出対象外です。",
                "Checks whether MonoBehaviours in the specified folders are placed in any scene or prefab.\n" +
                "Note: Dynamic usage via AddComponent<T>() or GetComponent<T>() is not detected."),
            new(LabelKey.CheckTargetFolder,     "チェック対象フォルダ",                          "Target Folders to Check"),
            new(LabelKey.CheckTargetFolderHint, "チェック対象のフォルダを1つ以上指定してください。", "Please specify at least one target folder."),
            new(LabelKey.FolderHint,            "フォルダ指定（空で全フォルダ）",                  "Folders (empty = all)"),
            new(LabelKey.TabUsed,               "使用中",                                       "Used"),
            new(LabelKey.TabUnused,             "未使用",                                       "Unused"),
            new(LabelKey.NoUsedScripts,         "使用中のスクリプトはありません。",                 "No used scripts found."),
            new(LabelKey.NoUnusedScripts,       "未使用のスクリプトはありません。",                 "No unused scripts found."),

            // --- Favorite Asset ---
            new(LabelKey.FavoriteAssetDragHint, "アセットをここにドラッグして登録",     "Drag assets here to register"),
            new(LabelKey.FavoriteAssetList,     "お気に入り一覧",                    "Favorites"),
            new(LabelKey.TargetCategory,        "登録先カテゴリ",                    "Target Category"),
            new(LabelKey.NewCategory,           "新規カテゴリ",                      "New Category"),
            new(LabelKey.DefaultCategory,       "未分類",                           "Uncategorized"),
            new(LabelKey.ConfirmClearFavorites,  "お気に入りを全て削除しますか？",      "Delete all favorites?"),
            new(LabelKey.Confirm,               "確認",                             "Confirm"),
            new(LabelKey.NotFound,              "(見つかりません)",                  "(Not found)"),

            // --- Hierarchy Favorite ---
            new(LabelKey.HierarchyFavoriteTitle,    "Hierarchy Favorite",                                                                          "Hierarchy Favorite"),
            new(LabelKey.HierarchyFavoriteDragHint, "Hierarchy の GameObject をここにドラッグ",                                                      "Drag GameObjects from Hierarchy here"),
            new(LabelKey.SceneNotSavedMessage,      "シーンが未保存のためお気に入りに追加できません。先にシーンを保存してください。",                        "Cannot add to favorites because the scene is unsaved. Please save the scene first."),
            new(LabelKey.EntryNotFoundMessage,      "お気に入りの GameObject が見つかりませんでした。\n削除されたか、シーンが未ロードの可能性があります。",   "The favorited GameObject was not found.\nIt may have been deleted or the scene is not loaded."),
            new(LabelKey.ParseFailedMessage,        "GlobalObjectId のパースに失敗しました。データが破損している可能性があります。",                       "Failed to parse GlobalObjectId. The data may be corrupted."),
            new(LabelKey.ConfirmClearAllBookmarks,  "全てのお気に入りを削除しますか？",                                                               "Delete all favorites?"),
            new(LabelKey.Memo,                      "メモ",                                                                                       "Memo"),
            new(LabelKey.Missing,                   "(Missing)",                                                                                  "(Missing)"),

            // --- Open Folder ---
            new(LabelKey.UnsupportedPlatform,       "このOSには対応していません",   "This OS is not supported."),
        };

        // ======================================================================
        // Initialization
        // ======================================================================

        static EditorToolLabels()
        {
            _isJapanese = EditorPrefs.GetString(LanguagePrefKey, "ja") == "ja";
            RebuildCachedArrays();
            ValidateLabels();
        }

        private static void ValidateLabels()
        {
            var expectedCount = (int)LabelKey.Count;
            Debug.Assert(AllLabels.Length == expectedCount,
                $"[EditorToolLabels] AllLabels.Length ({AllLabels.Length}) != LabelKey.Count ({expectedCount})");

            for (int i = 0; i < AllLabels.Length; i++)
            {
                Debug.Assert((int)AllLabels[i].Key == i,
                    $"[EditorToolLabels] AllLabels[{i}].Key is {AllLabels[i].Key} (expected index {i})");
            }
        }

        // ======================================================================
        // Language switch menu
        // ======================================================================

        [MenuItem("UniLab/Language Setting/Japanese", false, -100)]
        private static void SetJapanese()
        {
            EditorPrefs.SetString(LanguagePrefKey, "ja");
            _isJapanese = true;
            RebuildCachedArrays();
        }

        [MenuItem("UniLab/Language Setting/Japanese", true)]
        private static bool ValidateSetJapanese()
        {
            Menu.SetChecked("UniLab/Language Setting/Japanese", _isJapanese);
            return true;
        }

        [MenuItem("UniLab/Language Setting/English", false, -99)]
        private static void SetEnglish()
        {
            EditorPrefs.SetString(LanguagePrefKey, "en");
            _isJapanese = false;
            RebuildCachedArrays();
        }

        [MenuItem("UniLab/Language Setting/English", true)]
        private static bool ValidateSetEnglish()
        {
            Menu.SetChecked("UniLab/Language Setting/English", !_isJapanese);
            return true;
        }

        // ======================================================================
        // Core accessor — O(1) array index, zero GC
        // ======================================================================

        /// <summary>
        /// Gets the localized string for the specified label key.
        /// </summary>
        public static string Get(LabelKey key)
        {
            var entry = AllLabels[(int)key];
            return _isJapanese ? entry.Ja : entry.En;
        }

        /// <summary>
        /// Cached tab labels array for GUILayout.Toolbar (Used / Unused).
        /// </summary>
        public static string[] TabLabels => _cachedTabLabels;

        /// <summary>
        /// Cached sort mode labels array for EditorGUILayout.Popup.
        /// </summary>
        public static string[] SortModeLabels => _cachedSortModeLabels;

        private static void RebuildCachedArrays()
        {
            _cachedTabLabels = new[] { Get(LabelKey.TabUsed), Get(LabelKey.TabUnused) };
            _cachedSortModeLabels = new[] { Get(LabelKey.SortByPath), Get(LabelKey.SortBySize), Get(LabelKey.SortByExtension) };
        }

    }
}
