#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// ラン全体の健康状態を一目で判断できる値だけを集約し、比較コストを下げる。
    /// </summary>
    [Serializable]
    public sealed class PerformanceSummaryReport
    {
        /// <summary>
        /// 母数不足の誤読を避けるため、全体フレーム数を残す。
        /// </summary>
        public int frameCount;

        /// <summary>
        /// 平均傾向を把握しやすくするため保持する。
        /// </summary>
        public float frameMsAvg;

        /// <summary>
        /// 体感性能比較の主軸を揃えるため、全体でも p95 を残す。
        /// </summary>
        public float frameMsP95;

        /// <summary>
        /// 単発ハングに近い悪化も把握できるよう、最大値を残す。
        /// </summary>
        public float frameMsMax;

        /// <summary>
        /// 全体の確保量を先に見れば、詳細を見るべきランを絞り込める。
        /// </summary>
        public long gcAllocBytesTotal;

        /// <summary>
        /// 明示的な GC 発火の有無をラン単位でも追えるようにする。
        /// </summary>
        public int gcCollectionsTotal;

        /// <summary>
        /// 描画統計が取れる環境だけ比較対象にできるよう、未取得時は -1 を使う。
        /// </summary>
        public float drawCallsAvg;

        /// <summary>
        /// 一時的な描画増加の大きさを総括でも読めるようにする。
        /// </summary>
        public int drawCallsMax;

        /// <summary>
        /// SetPass の平均傾向を残し、描画設定切替の増加を見やすくする。
        /// </summary>
        public float setPassCallsAvg;

        /// <summary>
        /// 状態切替のスパイクを検知しやすくするため最大値を残す。
        /// </summary>
        public int setPassCallsMax;

        /// <summary>
        /// ステップごとの増分合計で、ラン全体のメモリ膨張量を把握する。
        /// </summary>
        public long memoryGrowthBytes;

        /// <summary>
        /// 増えた回数を数えることで、継続的な右肩上がりをリーク兆候として読めるようにする。
        /// </summary>
        public int memoryMonotonicGrowthSteps;

        /// <summary>
        /// 録画混在の計測かどうかを残し、比較時に除外判断できるようにする。
        /// </summary>
        public bool recordingActive;

        /// <summary>
        /// サマリー比較だけで重要な文脈が欠けないよう、必要最小限の値をひとまとめにする。
        /// </summary>
        public PerformanceSummaryReport(int totalFrameCount, float averageFrameMilliseconds, float percentile95FrameMilliseconds, float maxFrameMilliseconds, long totalGcAllocatedBytes, int totalGcCollectionCount, float averageDrawCalls, int maxDrawCalls, float averageSetPassCalls, int maxSetPassCalls, long totalMemoryGrowthBytes, int monotonicGrowthStepCount, bool isRecordingActive)
        {
            frameCount = totalFrameCount;
            frameMsAvg = averageFrameMilliseconds;
            frameMsP95 = percentile95FrameMilliseconds;
            frameMsMax = maxFrameMilliseconds;
            gcAllocBytesTotal = totalGcAllocatedBytes;
            gcCollectionsTotal = totalGcCollectionCount;
            drawCallsAvg = averageDrawCalls;
            drawCallsMax = maxDrawCalls;
            setPassCallsAvg = averageSetPassCalls;
            setPassCallsMax = maxSetPassCalls;
            memoryGrowthBytes = totalMemoryGrowthBytes;
            memoryMonotonicGrowthSteps = monotonicGrowthStepCount;
            recordingActive = isRecordingActive;
        }
    }
}
#endif
