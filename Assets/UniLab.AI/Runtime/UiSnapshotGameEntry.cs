#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// ゲーム固有状態を JSON 化しやすいキー値へ変換した 1 行です。
    /// `JsonUtility` が辞書を持てない制約を避けるため配列要素へ展開します。
    /// </summary>
    [Serializable]
    public sealed class UiSnapshotGameEntry
    {
        /// <summary>
        /// 状態名です。
        /// 利用側が意味のある短い識別子を選べるようにします。
        /// </summary>
        public string key;

        /// <summary>
        /// 状態値です。
        /// 型情報よりも AI が比較可能な文字列表現を優先します。
        /// </summary>
        public string value;
    }
}
#endif
