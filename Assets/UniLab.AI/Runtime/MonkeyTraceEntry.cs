#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 1 手ごとの観測結果を JSONL へ残し、クラッシュ直前の操作列を再現できるようにします。
    /// </summary>
    [Serializable]
    public sealed class MonkeyTraceEntry
    {
        /// <summary>
        /// 1 始まりの手番です。
        /// </summary>
        public int step;

        /// <summary>
        /// 操作時フレームです。
        /// </summary>
        public int frame;

        /// <summary>
        /// 操作した要素パスです。
        /// </summary>
        public string target;

        /// <summary>
        /// 操作前シーンです。
        /// </summary>
        public string beforeScene;

        /// <summary>
        /// 操作後シーンです。
        /// </summary>
        public string afterScene;

        /// <summary>
        /// スナップショット差分があったかどうかです。
        /// </summary>
        public bool changed;

        /// <summary>
        /// 違反があったかどうかです。
        /// </summary>
        public bool violation;

        /// <summary>
        /// 待機した秒数です。
        /// </summary>
        public float waitedSeconds;

        /// <summary>
        /// 違反説明です。
        /// </summary>
        public string message;
    }
}
#endif
