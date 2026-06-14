using System;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// <see cref="AssetVaultSetupSettings"/> の Inspector です。Local/Remote フォルダの意味を HelpBox で明示し、
    /// 未設定・フォルダ以外・Local/Remote の重複を警告します。フォルダ指定と Sync をこの1画面で完結させます。
    /// </summary>
    [CustomEditor(typeof(AssetVaultSetupSettings))]
    public sealed class AssetVaultSetupSettingsEditor : UnityEditor.Editor
    {
        private const string LocalFolderPropertyName = "_localFolder";
        private const string RemoteFolderPropertyName = "_remoteFolder";
        private const string HelpMessage =
            "Sync AssetResource が走査するルートフォルダを指定します。\n"
            + "・Local Folder【必須】: プレイヤー同梱。直下サブフォルダ → グループ Local_<名>\n"
            + "・Remote Folder【任意】: CDN 配信。直下サブフォルダ → グループ Remote_<名>\n"
            + "フォルダ名は分類に影響しません（スロット自体が Local/Remote を決めます）。フォルダを指定して下のボタンで同期します。";

        private const string LocalMissingMessage = "Local Folder が未設定です。Local は必須のため、フォルダを指定するまで Sync できません。";
        private const string LocalNotFolderMessage = "Local Folder にフォルダ以外が割り当てられています。フォルダを指定してください。";
        private const string RemoteNotFolderMessage = "Remote Folder にフォルダ以外が割り当てられています。フォルダを指定してください。";
        private const string OverlapMessage = "Local と Remote は別フォルダにしてください（同一・入れ子は二重登録やアドレス衝突を招きます）。";

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(HelpMessage, MessageType.Info);

            EditorGUILayout.Space();
            DrawDefaultInspector();

            var settings = (AssetVaultSetupSettings)target;
            var localPath = settings.LocalFolderPath;
            var remotePath = settings.RemoteFolderPath;
            var localMissing = string.IsNullOrEmpty(localPath);
            var hasOverlap = IsSameOrNested(localPath, remotePath);

            if (localMissing)
            {
                EditorGUILayout.HelpBox(LocalMissingMessage, MessageType.Warning);
            }

            // フィールドは割り当て済みだがフォルダとして解決できない（＝ファイル等）場合を明示する。
            if (IsAssignedButNotFolder(LocalFolderPropertyName, localPath))
            {
                EditorGUILayout.HelpBox(LocalNotFolderMessage, MessageType.Warning);
            }

            if (IsAssignedButNotFolder(RemoteFolderPropertyName, remotePath))
            {
                EditorGUILayout.HelpBox(RemoteNotFolderMessage, MessageType.Warning);
            }

            if (hasOverlap)
            {
                EditorGUILayout.HelpBox(OverlapMessage, MessageType.Warning);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(localMissing || hasOverlap))
            {
                if (GUILayout.Button("Sync AssetResource"))
                {
                    AssetVaultEditorOperations.SyncAssetResource();
                }
            }
        }

        private bool IsAssignedButNotFolder(string propertyName, string resolvedPath)
        {
            var property = serializedObject.FindProperty(propertyName);
            return property != null && property.objectReferenceValue != null && string.IsNullOrEmpty(resolvedPath);
        }

        private static bool IsSameOrNested(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }

            return a == b
                || a.StartsWith(b + "/", StringComparison.Ordinal)
                || b.StartsWith(a + "/", StringComparison.Ordinal);
        }
    }
}
