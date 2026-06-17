using System.Collections.Generic;
using UniLab.AssetVault.Debugging;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault の Addressables 構成を一望・操作する統合ダッシュボードです。
    /// Setup / Build / Debug Override / Status / Conventions の各セクションを提供し、
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

        // 日本語の利用ガイド。blob/HEAD はデフォルトブランチに自動解決されるため、ブランチ名のハードコードを避けられる。
        private const string DocumentationUrl = "https://github.com/MasaKoha/UniLab/blob/HEAD/docs/asset-vault-usage.md";

        private AssetVaultStatus _status;
        private bool _statusLoaded;
        private IReadOnlyList<AssetVaultViolation> _violations;
        private bool _violationsChecked;
        private Vector2 _scrollPosition;

        [MenuItem(WindowMenuPath, false, WindowMenuPriority)]
        private static void Open()
        {
            var window = GetWindow<AssetVaultWindow>();
            window.titleContent = new(WindowTitle);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshStatus();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawDocumentationSection();
            EditorGUILayout.Space(SectionSpacing);
            DrawSetupSection();
            EditorGUILayout.Space(SectionSpacing);
            DrawBuildSection();
            EditorGUILayout.Space(SectionSpacing);
            DrawDebugOverrideSection();
            EditorGUILayout.Space(SectionSpacing);
            DrawStatusSection();
            EditorGUILayout.Space(SectionSpacing);
            DrawConventionsSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawDocumentationSection()
        {
            DrawHeader("Documentation");
            // UI は英語だが詳細な日本語ガイドへ即アクセスできるようにする（フォントアトラス対策で日本語は docs に集約しているため）。
            if (DrawActionButton(
                "Open Usage Guide (JP)",
                "Open the Japanese AssetVault usage guide (docs/asset-vault-usage.md) on GitHub."))
            {
                Application.OpenURL(DocumentationUrl);
            }
        }

        private void DrawSetupSection()
        {
            DrawHeader("Setup");
            if (DrawActionButton(
                "Open Setup Settings",
                "Open AssetVaultSetupSettings. Configure Local/Remote folders and run Sync AssetResource from its Inspector."))
            {
                AssetVaultEditorOperations.OpenSetupSettings();
            }
        }

        private void DrawBuildSection()
        {
            DrawHeader("Build");
            if (DrawActionButton(
                "New Build",
                "Full Addressables build. Run on first setup or after group/convention changes (rebuilds content state). Aborts if fatal convention violations (e.g. duplicate addresses) exist."))
            {
                AssetVaultEditorOperations.BuildNew();
                RefreshStatus();
            }

            var canBuildContentUpdate = AssetVaultEditorOperations.CanBuildContentUpdate();
            using (new EditorGUI.DisabledScope(!canBuildContentUpdate))
            {
                if (DrawActionButton(
                    "Content Update (Diff)",
                    "Builds only the diff from the previous content state. Use to add/update assets for a shipped app (requires a prior New Build)."))
                {
                    AssetVaultEditorOperations.BuildContentUpdate();
                    RefreshStatus();
                }
            }

            if (!canBuildContentUpdate)
            {
                EditorGUILayout.HelpBox("content state file not found. Run New Build first.", MessageType.None);
            }
        }

        private void DrawDebugOverrideSection()
        {
            DrawHeader("Debug Override");
            // 常時表示テキストは ASCII に限定する（Editor フォントアトラス溢れ回避）。日本語の詳細は docs/asset-vault-usage.md 参照。
            EditorGUILayout.HelpBox(
                "Overrides AssetVaultRuntime.BaseUrl with the preset BaseUrl (development builds only). "
                + "Enable via Activate / Deactivate from code.",
                MessageType.Info);

            if (DrawActionButton(
                "Edit Presets",
                "Select the settings asset to edit environment presets (display name / BaseUrl) in the Inspector."))
            {
                Selection.activeObject = AssetVaultDebugEnvironmentSettings.GetOrCreate();
            }
        }

        private void DrawStatusSection()
        {
            DrawHeader("Status");
            if (DrawActionButton(
                "Refresh",
                "Re-fetch the current Addressables configuration (RemoteLoadPath, Local/Remote group counts, AssetResource folder)."))
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
                EditorGUILayout.LabelField("Local Folder", string.IsNullOrEmpty(_status.LocalFolderPath) ? "Not set" : _status.LocalFolderPath);
                EditorGUILayout.LabelField("Remote Folder", string.IsNullOrEmpty(_status.RemoteFolderPath) ? "Not set (optional)" : _status.RemoteFolderPath);
            }
        }

        private void DrawConventionsSection()
        {
            DrawHeader("Conventions");
            if (DrawActionButton(
                "Check Conventions",
                "Check management groups for convention violations (duplicate address / orphan label / dependency registered as entry). Complements Addressables Analyze with AssetVault-specific checks."))
            {
                _violations = AssetVaultEditorOperations.CheckConventions();
                _violationsChecked = true;
                Repaint();
            }

            if (!_violationsChecked)
            {
                return;
            }

            if (_violations.Count == 0)
            {
                EditorGUILayout.HelpBox("No convention violations found.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{_violations.Count} convention violation(s)", EditorStyles.miniBoldLabel);
            foreach (var violation in _violations)
            {
                // 重大度は AssetVaultViolation.IsError に一元化されており、ビルド前ゲートと同じ判定を使う。
                var messageType = violation.IsError ? MessageType.Error : MessageType.Warning;
                EditorGUILayout.HelpBox($"[{violation.ViolationType}] {violation.Message}", messageType);
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
        /// 操作ボタンを描画します。説明はホバー時のツールチップで示します。
        /// 注意: 説明文を常時ラベル表示していたが、Unity の Editor 動的フォントアトラスが大量の日本語グリフで溢れ、
        /// ウィンドウ下部の文字が欠ける問題が出たため、同時描画するグリフ数を抑える目的でツールチップのみに変更した。
        /// </summary>
        /// <returns>ボタンが押された場合は true。</returns>
        private static bool DrawActionButton(string label, string description)
        {
            return GUILayout.Button(new GUIContent(label, description));
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
