namespace UniLab.Native.Haptics.Platform.Interface
{
    /// <summary>
    /// 触覚フィードバックを再生するプラットフォーム実装。
    /// </summary>
    internal interface IPlatformHaptic
    {
        /// <summary>
        /// 意味的なハプティクスを再生する。
        /// </summary>
        /// <param name="type">触覚フィードバックの種類。</param>
        void Play(HapticType type);

        /// <summary>
        /// 生の一発振動を再生する。
        /// </summary>
        /// <param name="milliseconds">振動時間。</param>
        void Vibrate(int milliseconds);
    }
}
