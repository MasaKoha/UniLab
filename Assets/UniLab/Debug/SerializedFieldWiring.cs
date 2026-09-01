using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.Diagnostics
{
    /// <summary>
    /// シリアライズ対象フィールドの結線状態です。
    /// </summary>
    [Serializable]
    public sealed class SerializedFieldWiring
    {
        /// <summary>
        /// コンポーネント型名です。
        /// </summary>
        public string componentTypeName;

        /// <summary>
        /// フィールド名です。
        /// </summary>
        public string fieldName;

        /// <summary>
        /// null かどうかです。
        /// </summary>
        public bool isNull;
    }
}
#endif
