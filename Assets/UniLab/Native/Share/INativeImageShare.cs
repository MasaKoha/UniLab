using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Native.Gallery;
using UnityEngine;

namespace UniLab.Native.Share
{
    /// <summary>
    /// 画像の撮影・端末ギャラリー保存・OS ネイティブ共有を、用途別の関数として提供する。
    /// 「保存だけ」「共有だけ」「保存して共有」を関数レベルで分けている。
    /// </summary>
    public interface INativeImageShare
    {
        /// <summary>
        /// 全画面スクリーンショットを撮影し、端末のギャラリーへ保存する（共有はしない）。
        /// </summary>
        /// <param name="fileName">PNG ファイル名。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存の成否と保存先を表す結果。</returns>
        UniTask<GallerySaveResult> SaveScreenshotToGalleryAsync(
            string fileName,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 全画面スクリーンショットを撮影し、必要ならギャラリー保存したうえで OS ネイティブ共有を開く。
        /// </summary>
        /// <param name="fileName">PNG ファイル名。</param>
        /// <param name="content">共有本文。画像パスは内部で設定する。</param>
        /// <param name="alsoSaveToGallery">共有前に端末ギャラリーへも保存するか。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>ギャラリー保存の結果。alsoSaveToGallery が false のときは保存元パスのみ設定した結果。</returns>
        UniTask<GallerySaveResult> ShareScreenshotAsync(
            string fileName,
            ShareContent content,
            bool alsoSaveToGallery = true,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Texture2D を端末のギャラリーへ保存する（共有はしない）。
        /// </summary>
        /// <param name="texture">保存するテクスチャ。</param>
        /// <param name="fileName">PNG ファイル名。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存の成否と保存先を表す結果。</returns>
        UniTask<GallerySaveResult> SaveTextureToGalleryAsync(
            Texture2D texture,
            string fileName,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Texture2D を必要ならギャラリー保存したうえで OS ネイティブ共有を開く。
        /// </summary>
        /// <param name="texture">共有するテクスチャ。</param>
        /// <param name="fileName">PNG ファイル名。</param>
        /// <param name="content">共有本文。画像パスは内部で設定する。</param>
        /// <param name="alsoSaveToGallery">共有前に端末ギャラリーへも保存するか。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>ギャラリー保存の結果。alsoSaveToGallery が false のときは保存元パスのみ設定した結果。</returns>
        UniTask<GallerySaveResult> ShareTextureAsync(
            Texture2D texture,
            string fileName,
            ShareContent content,
            bool alsoSaveToGallery = true,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default);
    }
}
