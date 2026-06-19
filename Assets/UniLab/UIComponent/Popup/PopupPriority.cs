namespace UniLab.UI.Popup
{
    /// <summary>
    /// ポップアップ表示要求の優先度。PopupService のキューイング順を決める。
    /// 数値が大きいほど優先され、同一優先度は要求順（FIFO）で処理される。
    /// </summary>
    public enum PopupPriority
    {
        /// <summary>未指定。最も低い扱い。</summary>
        None = 0,

        /// <summary>低優先度。</summary>
        Low,

        /// <summary>通常優先度。既定値として用いる。</summary>
        Normal,

        /// <summary>高優先度。</summary>
        High,

        /// <summary>システム通知（強制アップデート・メンテナンス等）。待機列の先頭に割り込む。</summary>
        System,
    }
}
