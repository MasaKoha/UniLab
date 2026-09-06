namespace UniLab.Native.Haptics
{
    /// <summary>
    /// 触覚フィードバックの意味的な種類。
    /// </summary>
    public enum HapticType
    {
        /// <summary>
        /// 再生しない。
        /// </summary>
        None = 0,

        /// <summary>
        /// 軽い衝撃。
        /// </summary>
        ImpactLight = 1,

        /// <summary>
        /// 中程度の衝撃。
        /// </summary>
        ImpactMedium = 2,

        /// <summary>
        /// 強い衝撃。
        /// </summary>
        ImpactHeavy = 3,

        /// <summary>
        /// 選択変更。
        /// </summary>
        Selection = 4,

        /// <summary>
        /// 成功通知。
        /// </summary>
        Success = 5,

        /// <summary>
        /// 警告通知。
        /// </summary>
        Warning = 6,

        /// <summary>
        /// 失敗通知。
        /// </summary>
        Error = 7,
    }
}
