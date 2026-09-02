using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 1シーン分の階層ダンプです。
    /// </summary>
    [Serializable]
    public sealed class SceneHierarchyScene
    {
        /// <summary>
        /// シーン名です。
        /// </summary>
        public string name;

        /// <summary>
        /// シーン内ノード一覧です。
        /// </summary>
        public SceneHierarchyNode[] nodes;
    }
}
#endif
