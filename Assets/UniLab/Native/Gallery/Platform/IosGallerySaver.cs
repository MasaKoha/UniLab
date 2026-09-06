#if UNITY_IOS
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Native.Gallery.Platform.Interface;
using UnityEngine;

namespace UniLab.Native.Gallery.Platform
{
    /// <summary>
    /// iOS の写真アプリへ画像を保存する。
    /// </summary>
    internal sealed class IosGallerySaver : IPlatformGallerySaver
    {
        [DllImport("__Internal")]
        private static extern bool _UniLabSaveImageToPhotos(string imagePath);

        /// <summary>
        /// PNG 画像を写真アプリへ保存する。
        /// </summary>
        /// <param name="imagePath">保存元 PNG の絶対パス。</param>
        /// <param name="fileName">端末側の表示ファイル名。iOS では使用しない。</param>
        /// <param name="albumRoot">保存先の領域。iOS は単一の写真ライブラリのみのため無視する。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存の成否と保存先を表す結果。iOS は保存元パスを保存先として返す。</returns>
        public UniTask<GallerySaveResult> SaveImageAsync(
            string imagePath,
            string fileName,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_UniLabSaveImageToPhotos(imagePath ?? string.Empty))
            {
                Debug.LogWarning($"[UniLab.Native] Failed to save image to Photos: {imagePath}");
                return UniTask.FromResult(GallerySaveResult.Failed(imagePath));
            }

            return UniTask.FromResult(GallerySaveResult.Succeeded(imagePath, imagePath));
        }
    }
}
#endif
