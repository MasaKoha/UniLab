using System.Collections.Generic;

namespace UniLab.AssetVault
{
    /// <summary>
    /// 起動シーケンスがダウンロード準備を続行するか判断できるよう、catalog 更新結果を保持します。
    /// </summary>
    public readonly struct CatalogUpdateInfo
    {
        /// <summary>
        /// catalog 確認で更新を検出し、適用したかどうかを取得します。
        /// </summary>
        public bool HasUpdate { get; }

        /// <summary>
        /// 呼び出し側がログ出力や変更内容の確認を行えるよう、確認中に更新された catalog 識別子を取得します。
        /// </summary>
        public IReadOnlyList<string> UpdatedCatalogIds { get; }

        /// <summary>
        /// vault service から起動シーケンスへ返す catalog 更新情報を作成します。
        /// </summary>
        public CatalogUpdateInfo(bool hasUpdate, IReadOnlyList<string> updatedCatalogIds)
        {
            HasUpdate = hasUpdate;
            UpdatedCatalogIds = updatedCatalogIds;
        }
    }
}
