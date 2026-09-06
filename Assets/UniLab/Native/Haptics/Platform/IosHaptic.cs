#if UNITY_IOS
using System.Runtime.InteropServices;
using UniLab.Native.Haptics.Platform.Interface;

namespace UniLab.Native.Haptics.Platform
{
    /// <summary>
    /// iOS のネイティブハプティクス実装。
    /// </summary>
    internal sealed class IosHaptic : IPlatformHaptic
    {
        [DllImport("__Internal")]
        private static extern void _UniLabHaptic(int type);

        [DllImport("__Internal")]
        private static extern void _UniLabVibrate(int milliseconds);

        /// <summary>
        /// 意味的なハプティクスを再生する。
        /// </summary>
        /// <param name="type">触覚フィードバックの種類。</param>
        public void Play(HapticType type)
        {
            _UniLabHaptic((int)type);
        }

        /// <summary>
        /// 生の一発振動を再生する。
        /// </summary>
        /// <param name="milliseconds">振動時間。</param>
        public void Vibrate(int milliseconds)
        {
            _UniLabVibrate(milliseconds);
        }
    }
}
#endif
