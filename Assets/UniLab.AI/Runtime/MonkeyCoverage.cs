#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 探索済み画面と押下済み要素を集計し、シード変更の効果を比較できるようにします。
    /// </summary>
    [Serializable]
    public sealed class MonkeyCoverage
    {
        /// <summary>
        /// 訪問画面キーです。
        /// </summary>
        public string[] visitedScreens;

        /// <summary>
        /// 押した要素のパス一覧です。
        /// </summary>
        public string[] pressedElements;
    }
}
#endif
