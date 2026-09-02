#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 視覚回帰の判定件数だけを要約し、差分画像を開かなくてもラン一覧で異常量を判断できるようにする。
    /// </summary>
    [Serializable]
    public sealed class RunArchiveVisualRegressionSummary
    {
        /// <summary>
        /// 正常件数を要約へ含め、比較対象枚数の十分性を一覧で確認できるようにする。
        /// </summary>
        public int pass;

        /// <summary>
        /// 失敗件数を一覧へ持ち上げ、差分画像を開く優先度を即決できるようにする。
        /// </summary>
        public int fail;

        /// <summary>
        /// 初回導入と回帰失敗を区別できるよう、ベースライン未整備を別件数にする。
        /// </summary>
        public int noBaseline;

        /// <summary>
        /// 要約の既定値を構築時に確定し、後続の null 分岐をなくす。
        /// </summary>
        public RunArchiveVisualRegressionSummary(int passedCount, int failedCount, int missingBaselineCount)
        {
            pass = passedCount;
            fail = failedCount;
            noBaseline = missingBaselineCount;
        }
    }
}
#endif
