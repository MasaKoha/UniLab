using System.Collections.Generic;

namespace UniLab.AssetVault
{
    /// <summary>
    /// 「カタログ更新確認 → 差分の事前ダウンロード」を束ねた RunUpdateAsync の最終結果を表します。
    /// 起動シーケンスがカタログ更新の有無と、後続ダウンロードの結果をまとめて受け取れるようにします。
    /// </summary>
    public readonly struct AssetVaultUpdateResult
    {
        /// <summary>
        /// catalog 確認で更新を検出・適用したかどうかを取得します。CheckForUpdatesAsync の HasUpdate を反映します。
        /// </summary>
        public bool CatalogUpdated { get; }

        /// <summary>
        /// 呼び出し側がログ出力や変更内容の確認を行えるよう、確認中に更新された catalog 識別子を取得します。
        /// </summary>
        public IReadOnlyList<string> UpdatedCatalogIds { get; }

        /// <summary>
        /// カタログ確認後に実行した事前ダウンロードフロー（サイズ確認 → ユーザー確認 → リトライ付き DL）の結果を取得します。
        /// </summary>
        public AssetVaultDownloadResult DownloadResult { get; }

        /// <summary>
        /// カタログ更新結果と後続ダウンロード結果をまとめた更新結果を作成します。
        /// </summary>
        public AssetVaultUpdateResult(
            bool catalogUpdated,
            IReadOnlyList<string> updatedCatalogIds,
            AssetVaultDownloadResult downloadResult)
        {
            CatalogUpdated = catalogUpdated;
            UpdatedCatalogIds = updatedCatalogIds;
            DownloadResult = downloadResult;
        }
    }
}
