#if UNITY_IOS
using System.Runtime.InteropServices;
using UniLab.Native.Share.Platform.Interface;

namespace UniLab.Native.Share.Platform
{
    /// <summary>
    /// iOS のネイティブ共有シート実装。
    /// </summary>
    internal sealed class IosShare : IPlatformShare
    {
        [DllImport("__Internal")]
        private static extern void _UniLabShare(string text, string url, string imagePath, string subject);

        /// <summary>
        /// UIActivityViewController を開く。
        /// </summary>
        /// <param name="content">共有内容。</param>
        public void Share(in ShareContent content)
        {
            _UniLabShare(content.Text ?? string.Empty, content.Url ?? string.Empty, content.ImagePath ?? string.Empty, content.Subject ?? string.Empty);
        }
    }
}
#endif
