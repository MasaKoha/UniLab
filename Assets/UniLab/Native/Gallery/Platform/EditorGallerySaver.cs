using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Native.Gallery.Platform.Interface;
using UnityEngine;

namespace UniLab.Native.Gallery.Platform
{
    /// <summary>
    /// エディタ向けの画像保存 no-op 実装。
    /// </summary>
    internal sealed class EditorGallerySaver : IPlatformGallerySaver
    {
        /// <summary>
        /// 保存内容をログへ出力する。
        /// </summary>
        /// <param name="imagePath">保存元 PNG の絶対パス。</param>
        /// <param name="fileName">端末側の表示ファイル名。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存元 PNG を保存先とみなした成功結果。</returns>
        public UniTask<GallerySaveResult> SaveImageAsync(
            string imagePath,
            string fileName,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Debug.Log($"[UniLab.Native] Save image to gallery: ImagePath={imagePath}, FileName={fileName}, AlbumRoot={albumRoot}");
            return UniTask.FromResult(GallerySaveResult.Succeeded(imagePath, imagePath));
        }
    }
}
