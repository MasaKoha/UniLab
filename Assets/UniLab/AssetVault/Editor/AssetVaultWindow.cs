using UniLab.AssetVault.Debugging;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault の Addressables 構成を一望・操作する統合ダッシュボードです。
    /// Setup / Build / Sample / Debug Override / Status の各セクションを提供し、
    /// 操作はすべて <see cref="AssetVaultEditorOperations"/> に委譲します。
    /// </summary>
    public sealed class AssetVaultWindow : EditorWindow
    {
        // 他の AssetVault メニュー（Build / Setup / Sample）と揃えて UniLab 配下に集約する。
        private const string WindowMenuPath = "UniLab/AssetVault/Dashboard";
        private const int WindowMenuPriority = 0;
        private const string WindowTitle = "Asset Vault";
        private const float SectionSpacing = 8f;
        private const float LabelWidth = 120f;

        // Sample は削除可能な別 asmdef のため、core Editor からは直接参照せず MenuItem 経由で疎結合に呼ぶ。
        private const string GeneratePlaceholderMenuPath = "UniLab/AssetVault/Sample/Generate Placeholder Asset";
        private const string SampleMenuMissingMessage = "Sample メニューが見つかりません（Sample を削除した可能性があります）。";

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
            DrawSampleSection();
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
                "Sync AssetResource",
                "設定アセットの同期ルール（フォルダ＋Local/Remote）を走査し、Addressables のグループ・アドレス・プロファイルパスを自動構成します。ルールやフォルダ内容を変えた後に実行します。"))
            {
                AssetVaultEditorOperations.SyncAssetResource();
                RefreshStatus();
            }

            if (DrawActionButton(
                "Open Setup Settings",
                "ルートフォルダ参照などを持つ設定アセット (AssetVaultSetupSettings) を選択して Inspector に表示します。"))
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

        private void DrawSampleSection()
        {
            DrawHeader("Sample");
            if (DrawActionButton(
                "Generate Placeholder Asset",
                "動作確認用のプレースホルダーアセットを生成します（Sample asmdef のメニュー経由。Sample 未導入時は警告のみ）。"))
            {
                if (!EditorApplication.ExecuteMenuItem(GeneratePlaceholderMenuPath))
                {
                    Debug.LogWarning(SampleMenuMissingMessage);
                    return;
                }

                RefreshStatus();
            }
        }

        private void DrawDebugOverrideSection()
        {
            DrawHeader("Debug Override");
            EditorGUILayout.HelpBox(
                "選択した環境プリセットで AssetVaultRuntime の BaseUrl / ContentPath を上書きします（別環境・別版の検証用）。"
                + "設定はアセットに保存され、Editor Play と development ビルドで適用されます（release では無効）。",
                MessageType.Info);

            var settings = AssetVaultDebugEnvironmentSettings.GetOrCreate();
            using (var changeCheck = new EditorGUI.ChangeCheckScope())
            {
                settings.OverrideEnabled = EditorGUILayout.ToggleLeft("Enable Override", settings.OverrideEnabled);

                var presets = settings.Presets;
                if (presets.Count <= 0)
                {
                    EditorGUILayout.HelpBox("プリセットが未登録です。設定アセットで環境を追加してください。", MessageType.Warning);
                }
                else
                {
                    using (new EditorGUI.DisabledScope(!settings.OverrideEnabled))
                    {
                        using (new LabelWidthScope(LabelWidth))
                        {
                            DrawPresetPopup(settings);
                            var preset = settings.ResolveSelectedPreset();
                            EditorGUILayout.LabelField("BaseUrl", preset != null ? preset.BaseUrl : string.Empty);
                            EditorGUILayout.LabelField("ContentPath", preset != null ? preset.ContentPath : string.Empty);
                        }
                    }
                }

                if (changeCheck.changed)
                {
                    EditorUtility.SetDirty(settings);
                }
            }

            if (DrawActionButton(
                "Edit Presets",
                "環境プリセット (表示名・BaseUrl・ContentPath) を編集する設定アセットを選択して Inspector に表示します。"))
            {
                Selection.activeObject = settings;
            }
        }

        private static void DrawPresetPopup(AssetVaultDebugEnvironmentSettings settings)
        {
            var presets = settings.Presets;
            var presetNames = new string[presets.Count];
            for (var index = 0; index < presets.Count; index++)
            {
                presetNames[index] = presets[index].DisplayName;
            }

            var selectedIndex = System.Array.IndexOf(presetNames, settings.SelectedPresetName);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            var newIndex = EditorGUILayout.Popup("Environment", selectedIndex, presetNames);
            settings.SelectedPresetName = presetNames[newIndex];
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
                EditorGUILayout.LabelField("Sync Rules", _status.SyncRuleCount.ToString());
                EditorGUILayout.LabelField("Valid Folders", _status.ValidFolderCount.ToString());
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
