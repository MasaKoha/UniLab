using UniLab.Common;
using UniLab.Native.Share.Platform;
using UniLab.Native.Share.Platform.Interface;
using UnityEngine;

namespace UniLab.Native.Share
{
    /// <summary>
    /// OS ネイティブ共有シートのファサード。
    /// </summary>
    public sealed class NativeShare : SingletonPureClass<NativeShare>, INativeShare
    {
        private IPlatformShare _platformShare;

        /// <summary>
        /// プラットフォーム実装を初期化する。
        /// </summary>
        public void Initialize()
        {
            if (Application.isEditor)
            {
                _platformShare = new EditorShare();
                return;
            }

            IPlatformShare platformShare = null;
#if UNITY_EDITOR
            platformShare = new EditorShare();
#elif UNITY_IOS
            platformShare = new IosShare();
#elif UNITY_ANDROID
            platformShare = new AndroidShare();
#else
            platformShare = new EditorShare();
#endif
            _platformShare = platformShare;
        }

        /// <summary>
        /// 共有シートを開く。
        /// </summary>
        /// <param name="content">共有内容。</param>
        public void Share(in ShareContent content)
        {
            EnsureInitialized();
            _platformShare.Share(content);
        }

        private void EnsureInitialized()
        {
            if (_platformShare == null)
            {
                Initialize();
            }
        }
    }
}
