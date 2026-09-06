using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.Native
{
    /// <summary>
    /// 共有画像用のスクリーンショットを一時ファイルへ保存する。
    /// </summary>
    public static class ScreenshotCapture
    {
        /// <summary>
        /// フレーム終端で画面を PNG 化し、一時ファイルのパスを返す。
        /// </summary>
        /// <param name="fileName">保存ファイル名。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存した PNG ファイルの絶対パス。</returns>
        public static async UniTask<string> CaptureToTempFileAsync(string fileName, CancellationToken cancellationToken = default)
        {
            await UniTask.WaitForEndOfFrame(cancellationToken);

            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                texture.Apply();
                return WriteTextureToTempFile(texture, fileName);
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        /// <summary>
        /// 指定した Texture2D を PNG 化し、一時ファイルのパスを返す。
        /// </summary>
        /// <param name="texture">保存するテクスチャ。</param>
        /// <param name="fileName">保存ファイル名。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存した PNG ファイルの絶対パス。</returns>
        public static UniTask<string> SaveTextureToTempFileAsync(
            Texture2D texture,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (texture == null)
            {
                throw new System.ArgumentNullException(nameof(texture));
            }

            return UniTask.FromResult(WriteTextureToTempFile(texture, fileName));
        }

        /// <summary>
        /// 専用カメラで描画した内容を PNG 化し、一時ファイルのパスを返す。
        /// </summary>
        /// <param name="camera">描画に使用するカメラ。</param>
        /// <param name="width">出力幅。</param>
        /// <param name="height">出力高さ。</param>
        /// <param name="fileName">保存ファイル名。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存した PNG ファイルの絶対パス。</returns>
        public static async UniTask<string> CaptureCameraAsync(
            Camera camera,
            int width,
            int height,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            await UniTask.WaitForEndOfFrame(cancellationToken);

            var renderTexture = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previousTargetTexture = camera.targetTexture;
            var previousActiveTexture = RenderTexture.active;

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return WriteTextureToTempFile(texture, fileName);
            }
            finally
            {
                camera.targetTexture = previousTargetTexture;
                RenderTexture.active = previousActiveTexture;
                UnityEngine.Object.Destroy(texture);
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
        }

        private static string WriteTextureToTempFile(Texture2D texture, string fileName)
        {
            var pngBytes = texture.EncodeToPNG();
            var path = Path.Combine(Application.temporaryCachePath, EnsurePngExtension(fileName));
            Directory.CreateDirectory(Application.temporaryCachePath);
            File.WriteAllBytes(path, pngBytes);
            return path;
        }

        private static string EnsurePngExtension(string fileName)
        {
            if (Path.GetExtension(fileName).ToLowerInvariant() == ".png")
            {
                return fileName;
            }

            return fileName + ".png";
        }
    }
}
