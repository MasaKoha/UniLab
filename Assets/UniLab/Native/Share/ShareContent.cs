namespace UniLab.Native.Share
{
    /// <summary>
    /// 共有ペイロード。
    /// </summary>
    public readonly struct ShareContent
    {
        /// <summary>
        /// 共有本文。
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// 共有 URL。
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// 共有画像の絶対ファイルパス。
        /// </summary>
        public string ImagePath { get; }

        /// <summary>
        /// Android の共有シート件名。
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// 共有ペイロードを作成する。
        /// </summary>
        /// <param name="text">共有本文。</param>
        /// <param name="url">共有 URL。</param>
        /// <param name="imagePath">共有画像の絶対ファイルパス。</param>
        /// <param name="subject">Android の共有シート件名。</param>
        public ShareContent(string text, string url = null, string imagePath = null, string subject = null)
        {
            Text = text;
            Url = url;
            ImagePath = imagePath;
            Subject = subject;
        }
    }
}
