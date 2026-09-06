using UniLab.Common;
using UniLab.Native.Haptics.Platform;
using UniLab.Native.Haptics.Platform.Interface;
using UnityEngine;

namespace UniLab.Native.Haptics
{
    /// <summary>
    /// 触覚フィードバックのファサード。
    /// </summary>
    public sealed class Haptic : SingletonPureClass<Haptic>, IHaptic
    {
        private IPlatformHaptic _platformHaptic;

        /// <summary>
        /// プラットフォーム実装を初期化する。
        /// </summary>
        public void Initialize()
        {
            if (Application.isEditor)
            {
                _platformHaptic = new EditorHaptic();
                return;
            }

            IPlatformHaptic platformHaptic = null;
#if UNITY_EDITOR
            platformHaptic = new EditorHaptic();
#elif UNITY_IOS
            platformHaptic = new IosHaptic();
#elif UNITY_ANDROID
            platformHaptic = new AndroidHaptic();
#else
            platformHaptic = new EditorHaptic();
#endif
            _platformHaptic = platformHaptic;
        }

        /// <summary>
        /// 意味的なハプティクスを再生する。
        /// </summary>
        /// <param name="type">触覚フィードバックの種類。</param>
        public void Play(HapticType type)
        {
            EnsureInitialized();
            _platformHaptic.Play(type);
        }

        /// <summary>
        /// 生の一発振動を再生する。
        /// </summary>
        /// <param name="milliseconds">振動時間。</param>
        public void Vibrate(int milliseconds)
        {
            EnsureInitialized();
            _platformHaptic.Vibrate(milliseconds);
        }

        private void EnsureInitialized()
        {
            if (_platformHaptic == null)
            {
                Initialize();
            }
        }
    }
}
