#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// ランダム探索の再現性と破壊的操作の回避を JSON から制御するための設定です。
    /// </summary>
    [Serializable]
    public sealed class MonkeyOptions
    {
        /// <summary>
        /// 同じ手順を再現できるようにするためのシードです。
        /// </summary>
        public int seed;

        /// <summary>
        /// 無限探索を避けるための最大手数です。
        /// </summary>
        public int maxSteps = 500;

        /// <summary>
        /// 放置実行でも終了するようにするための最大秒数です。
        /// </summary>
        public float maxSeconds = 300.0f;

        /// <summary>
        /// 破壊的操作を名前部分一致で避けるための除外語です。
        /// </summary>
        public string[] excludePathContains = { "Delete", "Reset", "Quit" };

        /// <summary>
        /// 違反時に最短の再現手順で止めたい検証用の指定です。
        /// </summary>
        public bool stopOnViolation = false;

        /// <summary>
        /// InputInjector 経由の生入力を混ぜ、実機経路の不具合も拾うための指定です。
        /// </summary>
        public bool useRawInput = true;

        /// <summary>
        /// 無反応判定まで待つ秒数です。
        /// </summary>
        public float noChangeTimeoutSeconds = 2.0f;
    }
}
#endif
