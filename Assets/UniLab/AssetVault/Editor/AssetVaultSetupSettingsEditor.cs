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
        // Sync AssetResource が走査するルートフォルダの説明。スロットが Local/Remote を決め、フォルダ名は分類に影響しない。
        private const string HelpMessage =
            "Specify the root folders that Sync AssetResource scans.\n"
            + "- Local Folder [Required]: bundled with the player. Direct subfolder -> group Local_<name>\n"
            + "- Remote Folder [Optional]: served from CDN. Direct subfolder -> group Remote_<name>\n"
            + "Folder names do not affect classification (the slot itself decides Local/Remote). Assign folders, then sync with the button below.";

        // Local 未設定の警告。Local は必須のためフォルダ指定まで Sync 不可。
        private const string LocalMissingMessage = "Local Folder is not set. Local is required, so you cannot sync until a folder is assigned.";
        private const string LocalNotFolderMessage = "Local Folder is assigned a non-folder asset. Please assign a folder.";
        private const string RemoteNotFolderMessage = "Remote Folder is assigned a non-folder asset. Please assign a folder.";
        // 同一・入れ子は二重登録やアドレス衝突を招くため、別フォルダを要求する。
        private const string OverlapMessage = "Local and Remote must be different folders (identical or nested folders cause duplicate registration and address collisions).";
        private const string AutoRegisterEnabledMessage = "Auto-registration is on. Additions/moves/deletions under Local/Remote are incrementally reflected to Addressables. Run Sync AssetResource when you need strict duplicate detection or a full cleanup.";
        private const string AutoRegisterDisabledMessage = "Auto-registration is off. Run Sync AssetResource below to reflect changes under Local/Remote.";

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

            if (settings.AutoRegisterOnAssetChange)
            {
                EditorGUILayout.HelpBox(AutoRegisterEnabledMessage, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(AutoRegisterDisabledMessage, MessageType.Info);
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
