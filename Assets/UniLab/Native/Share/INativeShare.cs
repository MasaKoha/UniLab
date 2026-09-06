namespace UniLab.Native.Share
{
    /// <summary>
    /// OS ネイティブの共有シートを開く。
    /// </summary>
    public interface INativeShare
    {
        /// <summary>
        /// 共有シートを開く。
        /// </summary>
        /// <param name="content">共有内容。</param>
        void Share(in ShareContent content);
    }
}
