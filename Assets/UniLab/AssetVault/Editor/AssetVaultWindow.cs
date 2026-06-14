using UniLab.AssetVault.Debugging;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault の Addressables 構成を一望・操作する統合ダッシュボードです。
    /// Setup / Build / Debug Override / Status の各セクションを提供し、
    /// 操作はすべて <see cref="AssetVaultEditorOperations"/> に委譲します。
    /// </summary>
    public sealed class AssetVaultWindow : EditorWindow
    {
        // 他の AssetVault メニュー（Build / Setup）と揃えて UniLab 配下に集約する。
        private const string WindowMenuPath = "UniLab/AssetVault/Dashboard";
        private const int WindowMenuPriority = 0;
        private const string WindowTitle = "Asset Vault";
        private const float SectionSpacing = 8f;
        private const float LabelWidth = 120f;

        private AssetVaultStatus _status;
        private bool _statusLoaded;
        private Vector2 _scrollPosition;

        [MenuItem(WindowMenuPath, false, WindowMenuPriority)]
        private static void Open()
        {
            var window = GetWindow<AssetVaultWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshStatus();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawSetupSection();
            EditorGUILayout.Space(SectionSpacing);
            DrawBuildSection();
            EditorGUILayout.Space(SectionSpacing);
            DrawDebugOverrideSection();
            EditorGUILayout.Space(SectionSpacing);
            DrawStatusSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSetupSection()
        {
            DrawHeader("Setup");
            if (DrawActionButton(
                "Open Setup Settings",
                "設定アセット (AssetVaultSetupSettings) を開きます。Local/Remote フォルダの指定と Sync AssetResource はこの Inspector で行います。"))
            {
                AssetVaultEditorOperations.OpenSetupSettings();
            }
        }

        private void DrawBuildSection()
        {
            DrawHeader("Build");
            if (DrawActionButton(
                "New Build",
                "Addressables を新規フルビルドします。初回、またはグループ構成・規約を変更したときに実行します（content state を作り直します）。"))
            {
                AssetVaultEditorOperations.BuildNew();
                RefreshStatus();
            }

            if (DrawActionButton(
                "Content Update (Diff)",
                "前回の content state からの差分だけをビルドします。配信済みアプリ向けにアセットを追加・更新するときに使います（先に New Build が必要）。"))
            {
                AssetVaultEditorOperations.BuildContentUpdate();
                RefreshStatus();
            }
        }

        private void DrawDebugOverrideSection()
        {
            DrawHeader("Debug Override");
            EditorGUILayout.HelpBox(
                "選択した環境プリセットの BaseUrl で AssetVaultRuntime.BaseUrl を上書きします（ContentPath=版は version.json 解決に任せる）。"
                + "Editor Play と development ビルドで適用され、release では無効です。"
                + "有効化・プリセット選択は UI からは行えません（AssetVaultDebugEnvironmentSettings.Activate / Deactivate をコードから呼ぶ）。",
                MessageType.Info);

            if (DrawActionButton(
                "Edit Presets",
                "環境プリセット (表示名・BaseUrl) を編集する設定アセットを選択して Inspector に表示します。"))
            {
                Selection.activeObject = AssetVaultDebugEnvironmentSettings.GetOrCreate();
            }
        }

        private void DrawStatusSection()
        {
            DrawHeader("Status");
            if (DrawActionButton(
                "Refresh",
                "現在の Addressables 構成 (RemoteLoadPath・Local/Remote グループ数・AssetResource フォルダ有無) を再取得します。"))
            {
                RefreshStatus();
            }

            if (!_statusLoaded)
            {
                return;
            }

            if (!_status.SettingsInitialized)
            {
                EditorGUILayout.HelpBox("Addressables settings are not initialized.", MessageType.Warning);
                return;
            }

            using (new LabelWidthScope(LabelWidth))
            {
                EditorGUILayout.LabelField("RemoteLoadPath", _status.RemoteLoadPath);
                EditorGUILayout.LabelField("Local Groups", _status.LocalGroupCount.ToString());
                EditorGUILayout.LabelField("Remote Groups", _status.RemoteGroupCount.ToString());
                EditorGUILayout.LabelField("Local Folder", string.IsNullOrEmpty(_status.LocalFolderPath) ? "未設定" : _status.LocalFolderPath);
                EditorGUILayout.LabelField("Remote Folder", string.IsNullOrEmpty(_status.RemoteFolderPath) ? "未設定（任意）" : _status.RemoteFolderPath);
            }
        }

        private void RefreshStatus()
        {
            _status = AssetVaultEditorOperations.GetStatus();
            _statusLoaded = true;
            Repaint();
        }

        private static void DrawHeader(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        /// <summary>
        /// 操作ボタンと、その下に折り返し表示する説明文を描画します。説明はホバー時のツールチップにも使います。
        /// </summary>
        /// <returns>ボタンが押された場合は true。</returns>
        private static bool DrawActionButton(string label, string description)
        {
            var clicked = GUILayout.Button(new GUIContent(label, description));
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            return clicked;
        }

        /// <summary>
        /// IMGUI の EditorGUIUtility.labelWidth を一時的に変更し、Dispose で元に戻すスコープです。
        /// </summary>
        private readonly struct LabelWidthScope : System.IDisposable
        {
            private readonly float _previousLabelWidth;

            public LabelWidthScope(float labelWidth)
            {
                _previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = labelWidth;
            }

            public void Dispose()
            {
                EditorGUIUtility.labelWidth = _previousLabelWidth;
            }
        }
    }
}
