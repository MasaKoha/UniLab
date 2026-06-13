using System;

namespace UniLab.AssetDelivery
{
    /// <summary>
    /// アプリケーションの起動処理やロードフローが、再試行可能な基盤エラーとして扱うべき配信失敗を表します。
    /// </summary>
    public class AssetDeliveryException : Exception
    {
        /// <summary>
        /// 呼び出し側に提示する失敗メッセージを持つ asset delivery 例外を作成します。
        /// </summary>
        public AssetDeliveryException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 診断用に、原因となったプラットフォームまたは Addressables の失敗を保持する asset delivery 例外を作成します。
        /// </summary>
        public AssetDeliveryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
