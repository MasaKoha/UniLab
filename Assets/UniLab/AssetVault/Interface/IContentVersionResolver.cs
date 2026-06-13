using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.AssetVault
{
    /// <summary>
    /// version.json 等から現在のコンテンツ版を解決します。実装はアプリ層や BFF が差し替え可能です。IAssetVaultService.InitializeAsync の前に呼び、結果を AssetVaultRuntime にセットします。
    /// </summary>
    public interface IContentVersionResolver
    {
        /// <summary>
        /// 現在有効なコンテンツ版を非同期で解決します。
        /// </summary>
        UniTask<ContentVersionInfo> ResolveAsync(CancellationToken cancellationToken);
    }
}
