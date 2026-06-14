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
            if (GUILayout.Button("Sync AssetResource"))
            {
                AssetVaultEditorOperations.SyncAssetResource();
                RefreshStatus();
            }

            if (GUILayout.Button("Open Setup Settings"))
            {
                AssetVaultEditorOperations.OpenSetupSettings();
            }
        }

        private void DrawBuildSection()
        {
            DrawHeader("Build");
            if (GUILayout.Button("New Build"))
            {
                AssetVaultEditorOperations.BuildNew();
                RefreshStatus();
            }

            if (GUILayout.Button("Content Update (Diff)"))
            {
                AssetVaultEditorOperations.BuildContentUpdate();
                RefreshStatus();
            }
        }

        private void DrawSampleSection()
        {
            DrawHeader("Sample");
            if (GUILayout.Button("Generate Placeholder Asset"))
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
                "Play 突入時に、選択した環境プリセットで AssetVaultRuntime の BaseUrl / ContentPath を上書きします（別環境・別版の検証用）。",
                MessageType.Info);

            AssetVaultDebugOverride.Enabled = EditorGUILayout.ToggleLeft("Enable Override", AssetVaultDebugOverride.Enabled);

            var settings = AssetVaultDebugEnvironmentSettings.GetOrCreate();
            var presets = settings.Presets;
            if (presets.Count <= 0)
            {
                EditorGUILayout.HelpBox("プリセットが未登録です。設定アセットで環境を追加してください。", MessageType.Warning);
                if (GUILayout.Button("Edit Presets"))
                {
                    Selection.activeObject = settings;
                }

                return;
            }

            using (new EditorGUI.DisabledScope(!AssetVaultDebugOverride.Enabled))
            {
                using (new LabelWidthScope(LabelWidth))
                {
                    DrawPresetPopup(presets);
                    var preset = AssetVaultDebugOverride.ResolveSelectedPreset();
                    EditorGUILayout.LabelField("BaseUrl", preset != null ? preset.BaseUrl : string.Empty);
                    EditorGUILayout.LabelField("ContentPath", preset != null ? preset.ContentPath : string.Empty);
                }
            }

            if (GUILayout.Button("Edit Presets"))
            {
                Selection.activeObject = settings;
            }
        }

        private static void DrawPresetPopup(System.Collections.Generic.IReadOnlyList<AssetVaultDebugEnvironmentPreset> presets)
        {
            var presetNames = new string[presets.Count];
            for (var index = 0; index < presets.Count; index++)
            {
                presetNames[index] = presets[index].DisplayName;
            }

            var selectedName = AssetVaultDebugOverride.SelectedPresetName;
            var selectedIndex = System.Array.IndexOf(presetNames, selectedName);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            var newIndex = EditorGUILayout.Popup("Environment", selectedIndex, presetNames);
            AssetVaultDebugOverride.SelectedPresetName = presetNames[newIndex];
        }

        private void DrawStatusSection()
        {
            DrawHeader("Status");
            if (GUILayout.Button("Refresh"))
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
                EditorGUILayout.LabelField("AssetResource Root", _status.RootPath);
                EditorGUILayout.LabelField("Internal Folder", _status.InternalFolderExists ? "存在" : "なし");
                EditorGUILayout.LabelField("External Folder", _status.ExternalFolderExists ? "存在" : "なし");
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
