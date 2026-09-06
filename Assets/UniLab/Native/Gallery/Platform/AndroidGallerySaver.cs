#if UNITY_ANDROID
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Native.Gallery.Platform.Interface;
using UnityEngine;

namespace UniLab.Native.Gallery.Platform
{
    /// <summary>
    /// Android の MediaStore または DCIM/Pictures フォルダへ画像を保存する。
    /// </summary>
    internal sealed class AndroidGallerySaver : IPlatformGallerySaver
    {
        private const int ApiQ = 29;
        private const string AlbumName = "Questa";
        private const string MimeType = "image/png";

        /// <summary>
        /// PNG 画像を端末へ保存する。
        /// </summary>
        /// <param name="imagePath">保存元 PNG の絶対パス。</param>
        /// <param name="fileName">端末側の表示ファイル名。</param>
        /// <param name="albumRoot">保存先の領域（DCIM / Pictures）。既定は DCIM。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>保存の成否と保存先を表す結果。</returns>
        public async UniTask<GallerySaveResult> SaveImageAsync(
            string imagePath,
            string fileName,
            GalleryAlbumRoot albumRoot = GalleryAlbumRoot.Dcim,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(imagePath))
            {
                Debug.LogWarning($"[UniLab.Native] Image file does not exist: {imagePath}");
                return GallerySaveResult.Failed(imagePath);
            }

            try
            {
                // API 29+ の MediaStore は権限不要。API 28 以下のみ実行時に WRITE_EXTERNAL_STORAGE を要求する。
                if (GetApiLevel() >= ApiQ)
                {
                    return SaveWithMediaStore(imagePath, fileName, albumRoot);
                }

                return await SaveLegacyAsync(imagePath, fileName, albumRoot, cancellationToken);
            }
            catch (AndroidJavaException exception)
            {
                UnityEngine.Debug.LogException(exception);
                return GallerySaveResult.Failed(imagePath);
            }
            catch (IOException exception)
            {
                UnityEngine.Debug.LogException(exception);
                return GallerySaveResult.Failed(imagePath);
            }
        }

        private static int GetApiLevel()
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            return version.GetStatic<int>("SDK_INT");
        }

        private static GallerySaveResult SaveWithMediaStore(string imagePath, string fileName, GalleryAlbumRoot albumRoot)
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            using var mediaStoreImages = new AndroidJavaClass("android.provider.MediaStore$Images$Media");
            using var mediaColumns = new AndroidJavaClass("android.provider.MediaStore$MediaColumns");
            using var imageColumns = new AndroidJavaClass("android.provider.MediaStore$Images$ImageColumns");
            using var values = new AndroidJavaObject("android.content.ContentValues");
            var currentTimeMilliseconds = JavaCurrentTimeMillis();
            var currentTimeSeconds = currentTimeMilliseconds / 1000L;

            PutString(values, mediaColumns.GetStatic<string>("DISPLAY_NAME"), EnsurePngExtension(fileName));
            PutString(values, mediaColumns.GetStatic<string>("MIME_TYPE"), MimeType);
            PutString(values, mediaColumns.GetStatic<string>("RELATIVE_PATH"), ResolveRelativePath(albumRoot));
            PutLong(values, imageColumns.GetStatic<string>("DATE_TAKEN"), currentTimeMilliseconds);
            PutLong(values, mediaColumns.GetStatic<string>("DATE_ADDED"), currentTimeSeconds);
            PutLong(values, mediaColumns.GetStatic<string>("DATE_MODIFIED"), currentTimeSeconds);
            PutInt(values, mediaColumns.GetStatic<string>("IS_PENDING"), 1);

            using var uri = resolver.Call<AndroidJavaObject>(
                "insert",
                mediaStoreImages.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"),
                values);
            if (uri == null)
            {
                Debug.LogWarning("[UniLab.Native] MediaStore insert returned null.");
                return GallerySaveResult.Failed(imagePath);
            }

            WritePendingImage(resolver, uri, imagePath);

            using var updateValues = new AndroidJavaObject("android.content.ContentValues");
            PutInt(updateValues, mediaColumns.GetStatic<string>("IS_PENDING"), 0);
            resolver.Call<int>("update", uri, updateValues, null, null);
            return GallerySaveResult.Succeeded(uri.Call<string>("toString"), imagePath);
        }

        // MediaStore の OutputStream は close() で確定される。flush() だけでは finalize されないため必ず close する。
        private static void WritePendingImage(AndroidJavaObject resolver, AndroidJavaObject uri, string imagePath)
        {
            using var outputStream = resolver.Call<AndroidJavaObject>("openOutputStream", uri);
            // Java の byte[] は符号付き。Unity JNI へは sbyte[] で渡さないと余計な変換・警告が発生する。
            var pngBytes = ToSignedBytes(File.ReadAllBytes(imagePath));
            outputStream.Call("write", pngBytes);
            outputStream.Call("flush");
            outputStream.Call("close");
        }

        // perf: byte[] を sbyte[] へゼロコピー相当で写して JNI の byte[] シグネチャに一致させる。
        private static sbyte[] ToSignedBytes(byte[] source)
        {
            var signed = new sbyte[source.Length];
            System.Buffer.BlockCopy(source, 0, signed, 0, source.Length);
            return signed;
        }

        private static void PutString(AndroidJavaObject values, string key, string value)
        {
            values.Call("put", key, value);
        }

        private static void PutInt(AndroidJavaObject values, string key, int value)
        {
            using var boxedValue = new AndroidJavaObject("java.lang.Integer", value);
            values.Call("put", key, boxedValue);
        }

        private static void PutLong(AndroidJavaObject values, string key, long value)
        {
            using var boxedValue = new AndroidJavaObject("java.lang.Long", value);
            values.Call("put", key, boxedValue);
        }

        private static long JavaCurrentTimeMillis()
        {
            using var system = new AndroidJavaClass("java.lang.System");
            return system.CallStatic<long>("currentTimeMillis");
        }

        private static async UniTask<GallerySaveResult> SaveLegacyAsync(
            string imagePath,
            string fileName,
            GalleryAlbumRoot albumRoot,
            CancellationToken cancellationToken)
        {
            // 保存前に実行時権限を要求し、拒否されたらスキップする（未要求のまま黙ってスキップしない）。
            if (!await EnsureExternalStorageWritePermissionAsync(cancellationToken))
            {
                Debug.LogWarning("[UniLab.Native] WRITE_EXTERNAL_STORAGE is not granted. Gallery save is skipped.");
                return GallerySaveResult.Failed(imagePath);
            }

            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var environment = new AndroidJavaClass("android.os.Environment");
            using var rootDirectory = environment.CallStatic<AndroidJavaObject>(
                "getExternalStoragePublicDirectory",
                environment.GetStatic<string>(ResolveLegacyDirectoryField(albumRoot)));

            var questaDirectory = Path.Combine(rootDirectory.Call<string>("getAbsolutePath"), AlbumName);
            Directory.CreateDirectory(questaDirectory);

            var destinationPath = Path.Combine(questaDirectory, EnsurePngExtension(fileName));
            File.Copy(imagePath, destinationPath, true);

            using var mediaScanner = new AndroidJavaClass("android.media.MediaScannerConnection");
            mediaScanner.CallStatic("scanFile", activity, new[] { destinationPath }, new[] { MimeType }, null);
            return GallerySaveResult.Succeeded(destinationPath, imagePath);
        }

        /// <summary>
        /// API 28 以下のギャラリー保存に必要な WRITE_EXTERNAL_STORAGE を実行時に要求する。
        /// 既に許可済みなら即座に true。未許可ならダイアログを出し、その結果（許可/拒否）を待って返す。
        /// </summary>
        private static async UniTask<bool> EnsureExternalStorageWritePermissionAsync(CancellationToken cancellationToken)
        {
            var permission = UnityEngine.Android.Permission.ExternalStorageWrite;
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
            {
                return true;
            }

            // 権限ダイアログのコールバックはメインスレッドで発火する。結果を UniTask で待てるよう橋渡しする。
            var completionSource = new UniTaskCompletionSource<bool>();
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => completionSource.TrySetResult(true);
            callbacks.PermissionDenied += _ => completionSource.TrySetResult(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => completionSource.TrySetResult(false);

            // 待機中にキャンセルされたら拒否扱いで打ち切る。
            using (cancellationToken.Register(() => completionSource.TrySetResult(false)))
            {
                UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);
                return await completionSource.Task;
            }
        }

        // API 29+ の MediaStore RELATIVE_PATH（例 "DCIM/Questa"）。既定は DCIM。
        private static string ResolveRelativePath(GalleryAlbumRoot albumRoot)
        {
            var topDirectory = albumRoot == GalleryAlbumRoot.Pictures ? "Pictures" : "DCIM";
            return $"{topDirectory}/{AlbumName}";
        }

        // API 28 以下の Environment 定数名（DIRECTORY_DCIM / DIRECTORY_PICTURES）。既定は DCIM。
        private static string ResolveLegacyDirectoryField(GalleryAlbumRoot albumRoot)
        {
            return albumRoot == GalleryAlbumRoot.Pictures ? "DIRECTORY_PICTURES" : "DIRECTORY_DCIM";
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
#endif
