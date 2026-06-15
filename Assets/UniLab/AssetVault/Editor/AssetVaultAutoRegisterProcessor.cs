using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// Local/Remote ルート配下のインポートと移動を検知し、Addressables 登録を差分更新します。
    /// </summary>
    internal sealed class AssetVaultAutoRegisterProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!TryCreateContext(out var context))
            {
                return;
            }

            var hasRelevantChanges = HasRelevantChanges(context, importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            if (!hasRelevantChanges)
            {
                return;
            }

            var hasRegistrationChanges = HasRegistrationChanges(context, importedAssets, movedAssets);
            if (hasRegistrationChanges)
            {
                AssetVaultGroupRegistrar.EnsureProfileValues(context.AddressableAssetSettings);
            }

            // バッチ内で同一グループの EnsureGroup（スキーマ再設定）が繰り返されるのを防ぐためのキャッシュ。
            var groupCache = new Dictionary<string, AddressableAssetGroup>();
            AssetDatabase.StartAssetEditing();
            try
            {
                RegisterImportedAssets(context, importedAssets, groupCache);
                RegisterMovedAssets(context, movedAssets, movedFromAssetPaths, groupCache);
                AssetVaultGroupRegistrar.PruneEmptyManagedGroups(context.AddressableAssetSettings);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(context.AddressableAssetSettings);
            AssetDatabase.SaveAssets();
        }

        private static bool TryCreateContext(out ProcessorContext context)
        {
            context = default;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            if (!AssetVaultSetupSettings.TryLoad(out var setupSettings))
            {
                return false;
            }

            if (!setupSettings.AutoRegisterOnAssetChange)
            {
                return false;
            }

            if (!AddressableSettingsAccessor.TryGetSettingsSilently(out var addressableAssetSettings))
            {
                return false;
            }

            var localFolderPath = setupSettings.LocalFolderPath;
            if (string.IsNullOrEmpty(localFolderPath))
            {
                return false;
            }

            context = new ProcessorContext(
                addressableAssetSettings,
                localFolderPath,
                setupSettings.RemoteFolderPath);
            return true;
        }

        private static bool HasRelevantChanges(
            ProcessorContext context,
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var importedAsset in importedAssets)
            {
                if (TryResolveManagedRoot(context, importedAsset, out _, out _))
                {
                    return true;
                }
            }

            foreach (var deletedAsset in deletedAssets)
            {
                if (TryResolveManagedRoot(context, deletedAsset, out _, out _))
                {
                    return true;
                }
            }

            for (var index = 0; index < movedAssets.Length; index++)
            {
                if (TryResolveManagedRoot(context, movedAssets[index], out _, out _))
                {
                    return true;
                }

                if (index < movedFromAssetPaths.Length && TryResolveManagedRoot(context, movedFromAssetPaths[index], out _, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRegistrationChanges(ProcessorContext context, string[] importedAssets, string[] movedAssets)
        {
            foreach (var importedAsset in importedAssets)
            {
                if (TryResolveManagedRoot(context, importedAsset, out _, out _))
                {
                    return true;
                }
            }

            foreach (var movedAsset in movedAssets)
            {
                if (TryResolveManagedRoot(context, movedAsset, out _, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RegisterImportedAssets(ProcessorContext context, string[] importedAssets, IDictionary<string, AddressableAssetGroup> groupCache)
        {
            foreach (var importedAsset in importedAssets)
            {
                if (!TryResolveManagedRoot(context, importedAsset, out var categoryRoot, out var isLocal))
                {
                    continue;
                }

                RegisterPathOrFolder(context, importedAsset, categoryRoot, isLocal, groupCache);
            }
        }

        private static void RegisterMovedAssets(
            ProcessorContext context,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            IDictionary<string, AddressableAssetGroup> groupCache)
        {
            for (var index = 0; index < movedAssets.Length; index++)
            {
                var movedAsset = movedAssets[index];
                if (TryResolveManagedRoot(context, movedAsset, out var categoryRoot, out var isLocal))
                {
                    RegisterPathOrFolder(context, movedAsset, categoryRoot, isLocal, groupCache);
                    continue;
                }

                if (index >= movedFromAssetPaths.Length)
                {
                    continue;
                }

                if (!TryResolveManagedRoot(context, movedFromAssetPaths[index], out _, out _))
                {
                    continue;
                }

                // ルート外へ出たので登録解除する。フォルダ移動なら配下アセットをまとめて外す。
                RemovePathOrFolder(context, movedAsset);
            }
        }

        // assetPath がフォルダなら配下の全アセットを、ファイルなら自身を登録する（フォルダのリネーム/移動でも子の所属・アドレス・ラベルを追従させる）。
        private static void RegisterPathOrFolder(ProcessorContext context, string assetPath, string categoryRoot, bool isLocal, IDictionary<string, AddressableAssetGroup> groupCache)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                foreach (var childAssetPath in AssetVaultGroupRegistrar.EnumerateFolderAssetPaths(assetPath))
                {
                    AssetVaultGroupRegistrar.RegisterSingle(context.AddressableAssetSettings, childAssetPath, categoryRoot, isLocal, groupCache);
                }

                return;
            }

            AssetVaultGroupRegistrar.RegisterSingle(context.AddressableAssetSettings, assetPath, categoryRoot, isLocal, groupCache);
        }

        // assetPath がフォルダなら配下の全アセットのエントリを、ファイルなら自身のエントリを除去する。
        private static void RemovePathOrFolder(ProcessorContext context, string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                foreach (var childAssetPath in AssetVaultGroupRegistrar.EnumerateFolderAssetPaths(assetPath))
                {
                    var childGuid = AssetDatabase.AssetPathToGUID(childAssetPath);
                    AssetVaultGroupRegistrar.RemoveEntry(context.AddressableAssetSettings, childGuid);
                }

                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            AssetVaultGroupRegistrar.RemoveEntry(context.AddressableAssetSettings, guid);
        }

        private static bool TryResolveManagedRoot(ProcessorContext context, string assetPath, out string categoryRoot, out bool isLocal)
        {
            var normalizedAssetPath = AssetVaultAddressing.NormalizeAssetPath(assetPath);
            if (AssetVaultAddressing.IsUnderRoot(normalizedAssetPath, context.LocalFolderPath))
            {
                categoryRoot = context.LocalFolderPath;
                isLocal = true;
                return true;
            }

            if (AssetVaultAddressing.IsUnderRoot(normalizedAssetPath, context.RemoteFolderPath))
            {
                categoryRoot = context.RemoteFolderPath;
                isLocal = false;
                return true;
            }

            categoryRoot = string.Empty;
            isLocal = false;
            return false;
        }

        private readonly struct ProcessorContext
        {
            public ProcessorContext(
                UnityEditor.AddressableAssets.Settings.AddressableAssetSettings addressableAssetSettings,
                string localFolderPath,
                string remoteFolderPath)
            {
                AddressableAssetSettings = addressableAssetSettings;
                LocalFolderPath = AssetVaultAddressing.NormalizeAssetPath(localFolderPath);
                RemoteFolderPath = AssetVaultAddressing.NormalizeAssetPath(remoteFolderPath);
            }

            public UnityEditor.AddressableAssets.Settings.AddressableAssetSettings AddressableAssetSettings { get; }
            public string LocalFolderPath { get; }
            public string RemoteFolderPath { get; }
        }
    }
}
