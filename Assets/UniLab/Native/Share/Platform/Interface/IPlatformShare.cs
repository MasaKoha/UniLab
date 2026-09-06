namespace UniLab.Native.Share.Platform.Interface
{
    /// <summary>
    /// 共有シートを開くプラットフォーム実装。
    /// </summary>
    internal interface IPlatformShare
    {
        /// <summary>
        /// 共有シートを開く。
        /// </summary>
        /// <param name="content">共有内容。</param>
        void Share(in ShareContent content);
    }
}
