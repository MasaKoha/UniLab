using UnityEngine;
using UniLab.Native.Haptics.Platform.Interface;

namespace UniLab.Native.Haptics.Platform
{
    /// <summary>
    /// エディタ向けのハプティクス no-op 実装。
    /// </summary>
    internal sealed class EditorHaptic : IPlatformHaptic
    {
        /// <summary>
        /// ハプティクス種別をログへ出力する。
        /// </summary>
        /// <param name="type">触覚フィードバックの種類。</param>
        public void Play(HapticType type)
        {
            Debug.Log($"[UniLab.Native] Haptic: Type={type}");
        }

        /// <summary>
        /// 振動時間をログへ出力する。
        /// </summary>
        /// <param name="milliseconds">振動時間。</param>
        public void Vibrate(int milliseconds)
        {
            Debug.Log($"[UniLab.Native] Vibrate: Milliseconds={milliseconds}");
        }
    }
}
