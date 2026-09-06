using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Common;
using UniLab.Native.Gallery.Platform;
using UniLab.Native.Gallery.Platform.Interface;
using UnityEngine;

namespace UniLab.Native.Gallery
{
    /// <summary>
    /// 端末の写真アプリまたはギャラリー保存のファサード。
    /// </summary>
    public sealed class NativeGallerySaver : SingletonPureClass<NativeGallerySaver>, INativeGallerySaver
    {
        private IPlatformGallerySaver _platformGallerySaver;

        /// <summary>
        /// プラットフォーム実装を初期化する。
        /// </summary>
        public void Initialize()
        {
            if (Application.isEditor)
            {
                _platformGallerySaver = new EditorGallerySaver();
                return;
            }

            IPlatformGallerySaver platformGallerySaver = null;
#if UNITY_EDITOR
            platformGallerySaver = new EditorGallerySaver();
#elif UNITY_IOS
            platformGallerySaver = new IosGallerySaver();
#elif UNITY_ANDROID
            platformGallerySaver = new AndroidGallerySaver();
#else
            platformGallerySaver = new EditorGallerySaver();
#endif
            _platformGallerySaver = platformGallerySaver;
        }

        /// <summary>
        /// PNG 画像を端末へ保存する。
        /// </summary>
        /// <param name="imagePath">保存元 PNG の絶対パス。</param>
        /// <param name="fileName">端末側の表示ファイル名。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存の成否と保存先を表す結果。</returns>
        public UniTask<GallerySaveResult> SaveImageAsync(
            string imagePath,
            string fileName,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return _platformGallerySaver.SaveImageAsync(imagePath, fileName, albumRoot, cancellationToken);
        }

        private void EnsureInitialized()
        {
            if (_platformGallerySaver == null)
            {
                Initialize();
            }
        }
    }
}
