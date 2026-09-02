#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// JsonUtility がトップレベル配列を扱いにくいため、違反一覧を包む JSON モデルです。
    /// </summary>
    [Serializable]
    public sealed class MonkeyViolationList
    {
        /// <summary>
        /// 違反一覧です。
        /// </summary>
        public MonkeyViolation[] violations;
    }
}
#endif
