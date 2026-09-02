#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// どの操作が重かったかを再現なしで特定できるよう、ステップ単位の観測値を固定化する。
    /// </summary>
    [Serializable]
    public sealed class PerformanceStepReport
    {
        /// <summary>
        /// 元シナリオの並びと突き合わせるため、位置を数値で残す。
        /// </summary>
        public int index;

        /// <summary>
        /// ログや期待値の説明とつなげるため、人が読める識別子を保持する。
        /// </summary>
        public string label;

        /// <summary>
        /// 集計対象の十分性を判定できるよう、フレーム数を併記する。
        /// </summary>
        public int frameCount;

        /// <summary>
        /// 平均だけでは尾の重さを見落とすため、他の指標と併用する。
        /// </summary>
        public float frameMsAvg;

        /// <summary>
        /// 体感のカクつきに効く遅い側を拾うため、p95 を主指標にする。
        /// </summary>
        public float frameMsP95;

        /// <summary>
        /// 単発の大きな詰まりも見逃さないよう最大値を残す。
        /// </summary>
        public float frameMsMax;

        /// <summary>
        /// 毎フレームの小さな確保もホットパス劣化へ直結するため、合計 bytes を保持する。
        /// </summary>
        public long gcAllocBytes;

        /// <summary>
        /// 割り当てだけでなく実際の GC 発火も区別するため、回数を別持ちにする。
        /// </summary>
        public int gcCollections;

        /// <summary>
        /// 描画コストの平均傾向を把握しやすくするため、利用可能時のみ平均を残す。
        /// </summary>
        public float drawCallsAvg;

        /// <summary>
        /// 平均に埋もれるピーク負荷を見るため、最大値も保持する。
        /// </summary>
        public int drawCallsMax;

        /// <summary>
        /// ドローコールだけでは描画状態切替の重さを見誤るため、SetPass も別集計する。
        /// </summary>
        public float setPassCallsAvg;

        /// <summary>
        /// 一時的な描画スパイクを見逃さないため、SetPass の最大値も保持する。
        /// </summary>
        public int setPassCallsMax;

        /// <summary>
        /// ステップ境界のメモリ推移でリーク兆候を見るため、終了時総メモリを残す。
        /// </summary>
        public long totalMemoryBytes;

        /// <summary>
        /// `JsonUtility` の制約下でも後段が追加変換なしで扱える形に揃える。
        /// </summary>
        public PerformanceStepReport(int stepIndex, string stepLabel, int capturedFrameCount, float averageFrameMilliseconds, float percentile95FrameMilliseconds, float maxFrameMilliseconds, long gcAllocatedBytes, int gcCollectionCount, float averageDrawCalls, int maxDrawCalls, float averageSetPassCalls, int maxSetPassCalls, long totalAllocatedMemoryBytes)
        {
            index = stepIndex;
            label = string.IsNullOrEmpty(stepLabel) ? string.Empty : stepLabel;
            frameCount = capturedFrameCount;
            frameMsAvg = averageFrameMilliseconds;
            frameMsP95 = percentile95FrameMilliseconds;
            frameMsMax = maxFrameMilliseconds;
            gcAllocBytes = gcAllocatedBytes;
            gcCollections = gcCollectionCount;
            drawCallsAvg = averageDrawCalls;
            drawCallsMax = maxDrawCalls;
            setPassCallsAvg = averageSetPassCalls;
            setPassCallsMax = maxSetPassCalls;
            totalMemoryBytes = totalAllocatedMemoryBytes;
        }
    }
}
#endif
