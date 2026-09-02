#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// モンキー実行の終了理由と成果物を呼び出し元へ返すための JSON モデルです。
    /// </summary>
    [Serializable]
    public sealed class MonkeySummary
    {
        /// <summary>
        /// 実行シードです。
        /// </summary>
        public int seed;

        /// <summary>
        /// 実行した手数です。
        /// </summary>
        public int stepCount;

        /// <summary>
        /// 所要秒数です。
        /// </summary>
        public float durationSeconds;

        /// <summary>
        /// 違反数です。
        /// </summary>
        public int violationCount;

        /// <summary>
        /// 押した要素の種類数です。
        /// </summary>
        public int pressedElementCount;

        /// <summary>
        /// 訪問した画面キーの種類数です。
        /// </summary>
        public int visitedScreenCount;

        /// <summary>
        /// 停止理由です。
        /// </summary>
        public string stopReason;

        /// <summary>
        /// 出力ディレクトリです。
        /// </summary>
        public string outputDirectory;
    }
}
#endif
