namespace UniLab.UI.Focus
{
    /// <summary>
    /// フォーカス移動の方向。EventSystem のナビゲーションに頼らず、自前の方向解決で使う。
    /// </summary>
    public enum FocusDirection
    {
        /// <summary>方向入力なし。</summary>
        None = 0,

        /// <summary>上方向。</summary>
        Up = 1,

        /// <summary>下方向。</summary>
        Down = 2,

        /// <summary>左方向。</summary>
        Left = 3,

        /// <summary>右方向。</summary>
        Right = 4,
    }
}
