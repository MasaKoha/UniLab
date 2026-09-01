using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.Diagnostics
{
    /// <summary>
    /// 検出した破綻1件。
    /// </summary>
    [Serializable]
    public sealed class UiLayoutAuditEntry
    {
        /// <summary>
        /// 種別。TextOverflow / ClipOverflow / SiblingOverlap のいずれか。
        /// </summary>
        public string kind;

        /// <summary>
        /// ルートからのパスです。
        /// </summary>
        public string path;

        /// <summary>
        /// 人間向けの説明です。
        /// </summary>
        public string message;
    }
}
#endif
