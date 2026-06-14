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

            EditorGUILayout.Space();

            // 現在の選択・有効状態はコードでのみ変更できるため、読み取り専用で状態を示す。
            var settings = (AssetVaultDebugEnvironmentSettings)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Override Enabled", settings.OverrideEnabled);
                EditorGUILayout.TextField("Selected Preset", settings.SelectedPresetName);
            }
        }
    }
}
