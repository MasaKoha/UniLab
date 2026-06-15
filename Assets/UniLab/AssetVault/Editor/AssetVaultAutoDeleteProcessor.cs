using UnityEditor;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// Local/Remote ルート配下の削除前に guid を取得し、Addressables エントリを差分削除します。
    /// </summary>
    internal sealed class AssetVaultAutoDeleteProcessor : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            if (!TryCreateContext(out var context))
            {
                return AssetDeleteResult.DidNotDelete;
            }

            if (!IsUnderManagedRoot(context, assetPath))
            {
                return AssetDeleteResult.DidNotDelete;
            }

            // 削除前に guid を確定させる（削除後はパスから引けないため）。フォルダ削除なら配下アセットをまとめて外す。
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                foreach (var childAssetPath in AssetVaultGroupRegistrar.EnumerateFolderAssetPaths(assetPath))
                {
                    var childGuid = AssetDatabase.AssetPathToGUID(childAssetPath);
                    AssetVaultGroupRegistrar.RemoveEntry(context.AddressableAssetSettings, childGuid);
                }

                return AssetDeleteResult.DidNotDelete;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            AssetVaultGroupRegistrar.RemoveEntry(context.AddressableAssetSettings, guid);
            return AssetDeleteResult.DidNotDelete;
        }

        private static bool TryCreateContext(out DeleteProcessorContext context)
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

            context = new DeleteProcessorContext(
                addressableAssetSettings,
                localFolderPath,
                setupSettings.RemoteFolderPath);
            return true;
        }

        private static bool IsUnderManagedRoot(DeleteProcessorContext context, string assetPath)
        {
            var normalizedAssetPath = AssetVaultAddressing.NormalizeAssetPath(assetPath);
            if (AssetVaultAddressing.IsUnderRoot(normalizedAssetPath, context.LocalFolderPath))
            {
                return true;
            }

            return AssetVaultAddressing.IsUnderRoot(normalizedAssetPath, context.RemoteFolderPath);
        }

        private readonly struct DeleteProcessorContext
        {
            public DeleteProcessorContext(
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
