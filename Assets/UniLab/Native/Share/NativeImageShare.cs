using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Native.Gallery;
using UnityEngine;

namespace UniLab.Native.Share
{
    /// <summary>
    /// 撮影・ギャラリー保存・ネイティブ共有を用途別に組み合わせるファサード。
    /// 各操作（撮影＝ScreenshotCapture / 保存＝NativeGallerySaver / 共有＝NativeShare）を単一責務のまま合成する。
    /// </summary>
    public sealed class NativeImageShare : INativeImageShare
    {
        private readonly INativeGallerySaver _gallerySaver;
        private readonly INativeShare _share;

        /// <summary>保存と共有の実装を受け取る。差し替えのため具象へ直接依存しない。</summary>
        public NativeImageShare(INativeGallerySaver gallerySaver, INativeShare share)
        {
            _gallerySaver = gallerySaver;
            _share = share;
        }

        /// <summary>
        /// 全画面スクリーンショットを撮影し、端末のギャラリーへ保存する（共有はしない）。
        /// </summary>
        /// <param name="fileName">PNG ファイル名。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存の成否と保存先を表す結果。</returns>
        public async UniTask<GallerySaveResult> SaveScreenshotToGalleryAsync(
            string fileName,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default)
        {
            var imagePath = await ScreenshotCapture.CaptureToTempFileAsync(fileName, cancellationToken);
            return await _gallerySaver.SaveImageAsync(imagePath, fileName, albumRoot, cancellationToken);
        }

        /// <summary>
        /// 全画面スクリーンショットを撮影し、必要ならギャラリー保存したうえでネイティブ共有を開く。
        /// </summary>
        /// <param name="fileName">PNG ファイル名。</param>
        /// <param name="content">共有本文。画像パスは内部で設定する。</param>
        /// <param name="alsoSaveToGallery">共有前に端末ギャラリーへも保存するか。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>ギャラリー保存の結果。</returns>
        public async UniTask<GallerySaveResult> ShareScreenshotAsync(
            string fileName,
            ShareContent content,
            bool alsoSaveToGallery = true,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default)
        {
            var imagePath = await ScreenshotCapture.CaptureToTempFileAsync(fileName, cancellationToken);
            return await SaveIfRequestedAndShareAsync(imagePath, fileName, content, alsoSaveToGallery, albumRoot, cancellationToken);
        }

        /// <summary>
        /// Texture2D を端末のギャラリーへ保存する（共有はしない）。
        /// </summary>
        /// <param name="texture">保存するテクスチャ。</param>
        /// <param name="fileName">PNG ファイル名。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存の成否と保存先を表す結果。</returns>
        public async UniTask<GallerySaveResult> SaveTextureToGalleryAsync(
            Texture2D texture,
            string fileName,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default)
        {
            var imagePath = await ScreenshotCapture.SaveTextureToTempFileAsync(texture, fileName, cancellationToken);
            return await _gallerySaver.SaveImageAsync(imagePath, fileName, albumRoot, cancellationToken);
        }

        /// <summary>
        /// Texture2D を必要ならギャラリー保存したうえでネイティブ共有を開く。
        /// </summary>
        /// <param name="texture">共有するテクスチャ。</param>
        /// <param name="fileName">PNG ファイル名。</param>
        /// <param name="content">共有本文。画像パスは内部で設定する。</param>
        /// <param name="alsoSaveToGallery">共有前に端末ギャラリーへも保存するか。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>ギャラリー保存の結果。</returns>
        public async UniTask<GallerySaveResult> ShareTextureAsync(
            Texture2D texture,
            string fileName,
            ShareContent content,
            bool alsoSaveToGallery = true,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default)
        {
            var imagePath = await ScreenshotCapture.SaveTextureToTempFileAsync(texture, fileName, cancellationToken);
            return await SaveIfRequestedAndShareAsync(imagePath, fileName, content, alsoSaveToGallery, albumRoot, cancellationToken);
        }

        // 保存要求があればギャラリー保存し、その後にネイティブ共有を開く共通処理。
        private async UniTask<GallerySaveResult> SaveIfRequestedAndShareAsync(
            string imagePath,
            string fileName,
            ShareContent content,
            bool alsoSaveToGallery,
            GalleryAlbumRoot albumRoot,
            CancellationToken cancellationToken)
        {
            var saveResult = GallerySaveResult.Failed(imagePath);
            if (alsoSaveToGallery)
            {
                saveResult = await _gallerySaver.SaveImageAsync(imagePath, fileName, albumRoot, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var shareContent = new ShareContent(content.Text, content.Url, imagePath, content.Subject);
            _share.Share(shareContent);
            return saveResult;
        }
    }
}
