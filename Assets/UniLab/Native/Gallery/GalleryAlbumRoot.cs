namespace UniLab.Native.Gallery
{
    /// <summary>
    /// ギャラリー保存先のトップレベル領域。DCIM（カメラロール扱い）と Pictures を切り替える。
    /// iOS は単一の写真ライブラリのみのため、この指定は無視される。
    /// </summary>
    public enum GalleryAlbumRoot
    {
        /// <summary>未指定。既定の DCIM として扱う。</summary>
        None = 0,

        /// <summary>DCIM 配下。端末のカメラロールと同じ領域。基本はこちら。</summary>
        Dcim = 1,

        /// <summary>Pictures 配下。アプリ生成画像向けの領域。</summary>
        Pictures = 2,
    }
}
