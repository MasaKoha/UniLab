using UnityEngine;
using UniLab.Native.Share.Platform.Interface;

namespace UniLab.Native.Share.Platform
{
    /// <summary>
    /// エディタ向けの共有 no-op 実装。
    /// </summary>
    internal sealed class EditorShare : IPlatformShare
    {
        /// <summary>
        /// 共有内容をログへ出力する。
        /// </summary>
        /// <param name="content">共有内容。</param>
        public void Share(in ShareContent content)
        {
            Debug.Log($"[UniLab.Native] Share: Text={content.Text}, Url={content.Url}, ImagePath={content.ImagePath}, Subject={content.Subject}");
        }
    }
}
