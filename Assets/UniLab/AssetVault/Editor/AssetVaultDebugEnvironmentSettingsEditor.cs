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
        // デバッグ環境プリセットの編集方法と適用範囲を説明するヘルプ文言。
        private const string HelpMessage =
            "Edit debug environment presets (display name and BaseUrl). Only BaseUrl is overridden; ContentPath (version) is resolved from version.json.\n"
            + "Activation and selection cannot be done from the UI. Call AssetVaultDebugEnvironmentSettings.Activate(\"<name>\") / Deactivate() from code.\n"
            + "Applied only in Editor Play and development builds; the code is stripped entirely from release builds.";

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
                    // 表示名が空のプリセットがある。
                    issues.Add("A preset has an empty display name.");
                }
                else if (!seenNames.Add(preset.DisplayName))
                {
                    // 表示名が重複している。
                    issues.Add($"Duplicate display name: {preset.DisplayName}");
                }

                if (string.IsNullOrEmpty(preset.BaseUrl))
                {
                    // BaseUrl が空である。
                    issues.Add($"BaseUrl is empty: {(string.IsNullOrEmpty(preset.DisplayName) ? "(unnamed)" : preset.DisplayName)}");
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
