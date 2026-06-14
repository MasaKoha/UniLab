using System.Collections.Generic;
using System.Linq;
using UniLab.AssetVault.Debugging;
using UnityEditor;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// <see cref="AssetVaultDebugEnvironmentSettings"/> の Inspector です。プリセット一覧のみ編集可能にし、
    /// 有効化・選択はコード（Activate / Deactivate）専用のため読み取り専用で表示します。
    /// </summary>
    [CustomEditor(typeof(AssetVaultDebugEnvironmentSettings))]
    public sealed class AssetVaultDebugEnvironmentSettingsEditor : UnityEditor.Editor
    {
        private const string PresetsPropertyName = "_presets";
        private const string HelpMessage =
            "デバッグ環境プリセット（表示名・BaseUrl）を編集します。BaseUrl のみ上書きし、ContentPath（版）は version.json 解決に任せます。\n"
            + "有効化・選択は UI からは行えません。コードから AssetVaultDebugEnvironmentSettings.Activate(\"<名前>\") / Deactivate() を呼んでください。\n"
            + "Editor Play と development ビルドでのみ適用され、release ビルドではコードごとストリップされます。";

        private SerializedProperty _presetsProperty;

        private void OnEnable()
        {
            _presetsProperty = serializedObject.FindProperty(PresetsPropertyName);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(HelpMessage, MessageType.Info);

            serializedObject.Update();
            EditorGUILayout.PropertyField(_presetsProperty, true);
            serializedObject.ApplyModifiedProperties();

            DrawValidationWarnings((AssetVaultDebugEnvironmentSettings)target);

            EditorGUILayout.Space();

            // 現在の選択・有効状態はコードでのみ変更できるため、読み取り専用で状態を示す。
            var settings = (AssetVaultDebugEnvironmentSettings)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Override Enabled", settings.OverrideEnabled);
                EditorGUILayout.TextField("Selected Preset", settings.SelectedPresetName);
            }
        }

        // プリセットの空名・重複名・空 URL を警告する（実行時の解決失敗・誤環境ロードを未然に防ぐ）。
        private static void DrawValidationWarnings(AssetVaultDebugEnvironmentSettings settings)
        {
            var issues = new List<string>();
            var seenNames = new HashSet<string>();
            foreach (var preset in settings.Presets)
            {
                if (string.IsNullOrEmpty(preset.DisplayName))
                {
                    issues.Add("表示名が空のプリセットがあります。");
                }
                else if (!seenNames.Add(preset.DisplayName))
                {
                    issues.Add($"表示名が重複しています: {preset.DisplayName}");
                }

                if (string.IsNullOrEmpty(preset.BaseUrl))
                {
                    issues.Add($"BaseUrl が空です: {(string.IsNullOrEmpty(preset.DisplayName) ? "(無名)" : preset.DisplayName)}");
                }
            }

            if (issues.Count <= 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(string.Join("\n", issues.Distinct()), MessageType.Warning);
        }
    }
}
