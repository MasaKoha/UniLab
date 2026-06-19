namespace UniLab.UI.Popup
{
    /// <summary>
    /// 確認ポップアップ（ConfirmPopup）の応答結果。
    /// </summary>
    public enum PopupResult
    {
        /// <summary>未確定（既定値）。</summary>
        None = 0,

        /// <summary>確認（OK）。</summary>
        Confirm,

        /// <summary>キャンセル。</summary>
        Cancel,
    }
}
