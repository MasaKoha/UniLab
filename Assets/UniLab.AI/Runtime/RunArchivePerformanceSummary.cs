#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 性能レポートの主要指標だけを別型に切り出し、一覧表示が詳細 JSON の構造へ引きずられないようにする。
    /// </summary>
    [Serializable]
    public sealed class RunArchivePerformanceSummary
    {
        /// <summary>
        /// 体感性能比較の主軸だけを固定し、スマホ一覧でも過不足なく比較できるようにする。
        /// </summary>
        public float frameMsP95;

        /// <summary>
        /// 未計測時でも null 分岐なしで扱えるよう、単一値で構築できるようにする。
        /// </summary>
        public RunArchivePerformanceSummary(float percentile95FrameMilliseconds)
        {
            frameMsP95 = percentile95FrameMilliseconds;
        }
    }
}
#endif
