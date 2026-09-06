#if UNITY_ANDROID
using System.IO;
using UniLab.Native.Share.Platform.Interface;
using UnityEngine;

namespace UniLab.Native.Share.Platform
{
    /// <summary>
    /// Android のネイティブ共有シート実装。
    /// </summary>
    internal sealed class AndroidShare : IPlatformShare
    {
        private const string FileProviderSuffix = ".unilab.fileprovider";

        /// <summary>
        /// Android の ACTION_SEND Intent を開く。
        /// </summary>
        /// <param name="content">共有内容。</param>
        public void Share(in ShareContent content)
        {
            var payload = AndroidSharePayloadBuilder.Build(content);
            var subject = content.Subject;
            var imagePath = content.ImagePath;

            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() => StartShareActivity(payload, subject, imagePath)));
        }

        private static void StartShareActivity(AndroidSharePayload payload, string subject, string imagePath)
        {
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", payload.MimeType);

                if (!string.IsNullOrEmpty(payload.Body))
                {
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), payload.Body);
                }

                if (!string.IsNullOrEmpty(subject))
                {
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject);
                }

                if (payload.HasImage)
                {
                    if (File.Exists(imagePath))
                    {
                        using var uri = BuildContentUri(activity, imagePath);
                        intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uri);
                        intent.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION"));
                        SetClipData(activity, intent, uri, subject);
                    }
                    else
                    {
                        Debug.LogWarning($"[UniLab.Native] Share image file does not exist: {imagePath}");
                    }
                }

                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, subject ?? string.Empty);
                activity.Call("startActivity", chooser);
            }
            catch (AndroidJavaException exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        private static AndroidJavaObject BuildContentUri(AndroidJavaObject activity, string absolutePath)
        {
            using var applicationContext = activity.Call<AndroidJavaObject>("getApplicationContext");
            var authority = applicationContext.Call<string>("getPackageName") + FileProviderSuffix;
            using var file = new AndroidJavaObject("java.io.File", absolutePath);
            using var fileProvider = new AndroidJavaClass("androidx.core.content.FileProvider");
            return fileProvider.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file);
        }

        private static void SetClipData(
            AndroidJavaObject activity,
            AndroidJavaObject intent,
            AndroidJavaObject uri,
            string subject)
        {
            using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            using var clipDataClass = new AndroidJavaClass("android.content.ClipData");
            using var clipData = clipDataClass.CallStatic<AndroidJavaObject>(
                "newUri",
                resolver,
                string.IsNullOrEmpty(subject) ? "image" : subject,
                uri);
            intent.Call("setClipData", clipData);
        }
    }
}
#endif
