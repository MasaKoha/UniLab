namespace UniLab.UI.Focus
{
    /// <summary>
    /// フォーカス移動時に端でラップ（反対側へ回り込む）を許可する方向。
    /// [Flags] にはせず、Both を明示値として持つ単純な enum とする。
    /// </summary>
    public enum FocusWrapMode
    {
        /// <summary>ラップしない。</summary>
        None = 0,

        /// <summary>行内の左右端でのラップを許可する。</summary>
        Horizontal = 1,

        /// <summary>列方向の上下端でのラップを許可する。</summary>
        Vertical = 2,

        /// <summary>水平・垂直の両方でラップを許可する。</summary>
        Both = 3,
    }
}
