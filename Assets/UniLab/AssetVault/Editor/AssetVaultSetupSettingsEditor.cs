using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// <see cref="AssetVaultSetupSettings"/> の Inspector です。Local/Remote フォルダの意味を HelpBox で明示し、
    /// 必須の Local フォルダが未設定の場合は警告します。フォルダ指定と Sync をこの1画面で完結させます。
    /// </summary>
    [CustomEditor(typeof(AssetVaultSetupSettings))]
    public sealed class AssetVaultSetupSettingsEditor : UnityEditor.Editor
    {
        private const string HelpMessage =
            "Sync AssetResource が走査するルートフォルダを指定します。\n"
            + "・Local Folder【必須】: プレイヤー同梱。直下サブフォルダ → グループ Local_<名>\n"
            + "・Remote Folder【任意】: CDN 配信。直下サブフォルダ → グループ Remote_<名>\n"
            + "フォルダ名は分類に影響しません（スロット自体が Local/Remote を決めます）。フォルダを指定して下のボタンで同期します。";

        private const string LocalMissingMessage = "Local Folder が未設定です。Local は必須のため、フォルダを指定するまで Sync できません。";

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(HelpMessage, MessageType.Info);

            EditorGUILayout.Space();
            DrawDefaultInspector();

            var settings = (AssetVaultSetupSettings)target;
            var localMissing = string.IsNullOrEmpty(settings.LocalFolderPath);
            if (localMissing)
            {
                EditorGUILayout.HelpBox(LocalMissingMessage, MessageType.Warning);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(localMissing))
            {
                if (GUILayout.Button("Sync AssetResource"))
                {
                    AssetVaultEditorOperations.SyncAssetResource();
                }
            }
        }
    }
}
