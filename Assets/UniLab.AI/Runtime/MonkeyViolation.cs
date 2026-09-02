#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// モンキー探索で見つけた不変条件違反を成果物と結び付けるための JSON モデルです。
    /// </summary>
    [Serializable]
    public sealed class MonkeyViolation
    {
        /// <summary>
        /// 違反した手番です。
        /// </summary>
        public int step;

        /// <summary>
        /// 違反種別です。
        /// </summary>
        public string kind;

        /// <summary>
        /// 対象パスです。
        /// </summary>
        public string target;

        /// <summary>
        /// 診断メッセージです。
        /// </summary>
        public string message;

        /// <summary>
        /// フォレンジック出力ディレクトリです。
        /// </summary>
        public string forensicsPath;
    }
}
#endif
