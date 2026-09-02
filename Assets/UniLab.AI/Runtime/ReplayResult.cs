#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 再生終了時に件数と不一致をまとめ、期待結果の評価側へ一度で渡すための結果です。
    /// </summary>
    public sealed class ReplayResult
    {
        /// <summary>
        /// 実際に送出できた入力件数です。manifest の件数と比較して欠落を検知します。
        /// </summary>
        public int PlayedInputCount { get; }

        /// <summary>
        /// 条件待ちや control 解決の失敗があったかをまとめるためのフラグです。
        /// </summary>
        public bool HasMismatch { get; }

        /// <summary>
        /// 人間が即座に原因へ飛べるよう、最初の不一致理由を保持します。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 新しい再生結果を生成します。
        /// </summary>
        public ReplayResult(int playedInputCount, bool hasMismatch, string message)
        {
            PlayedInputCount = playedInputCount;
            HasMismatch = hasMismatch;
            Message = message ?? string.Empty;
        }
    }
}
#endif
