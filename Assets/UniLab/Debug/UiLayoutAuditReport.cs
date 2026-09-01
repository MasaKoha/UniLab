using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.Diagnostics
{
    /// <summary>
    /// 監査結果。JsonUtility でそのままシリアライズできる形にする。
    /// </summary>
    [Serializable]
    public sealed class UiLayoutAuditReport
    {
        /// <summary>
        /// 監査日時です。
        /// </summary>
        public string capturedAt;

        /// <summary>
        /// 監査時の画面幅です。
        /// </summary>
        public int screenWidth;

        /// <summary>
        /// 監査時の画面高さです。
        /// </summary>
        public int screenHeight;

        /// <summary>
        /// 検出した破綻一覧です。
        /// </summary>
        public UiLayoutAuditEntry[] entries;
    }
}
#endif
