using System;

namespace UniLab.AssetVault
{
    /// <summary>
    /// アプリケーションの起動処理やロードフローが、再試行可能な基盤エラーとして扱うべき配信失敗を表します。
    /// </summary>
    public class AssetVaultException : Exception
    {
        /// <summary>
        /// 呼び出し側に提示する失敗メッセージを持つ asset vault 例外を作成します。
        /// </summary>
        public AssetVaultException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 診断用に、原因となったプラットフォームまたは Addressables の失敗を保持する asset vault 例外を作成します。
        /// </summary>
        public AssetVaultException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
