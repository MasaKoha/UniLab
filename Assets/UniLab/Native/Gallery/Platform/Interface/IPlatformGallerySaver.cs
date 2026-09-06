using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.Native.Gallery.Platform.Interface
{
    /// <summary>
    /// 端末の写真アプリまたはギャラリー保存のプラットフォーム実装。
    /// </summary>
    internal interface IPlatformGallerySaver
    {
        /// <summary>
        /// PNG 画像を端末へ保存する。
        /// </summary>
        /// <param name="imagePath">保存元 PNG の絶対パス。</param>
        /// <param name="fileName">端末側の表示ファイル名。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存の成否と保存先を表す結果。</returns>
        UniTask<GallerySaveResult> SaveImageAsync(
            string imagePath,
            string fileName,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default);
    }
}
