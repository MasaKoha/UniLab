using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// シーン階層ダンプ全体です。
    /// </summary>
    [Serializable]
    public sealed class SceneHierarchyDump
    {
        /// <summary>
        /// ダンプ日時です。
        /// </summary>
        public string capturedAt;

        /// <summary>
        /// シーン単位のダンプ結果です。
        /// </summary>
        public SceneHierarchyScene[] scenes;
    }
}
#endif
