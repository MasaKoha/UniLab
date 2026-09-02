using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 録画中の特定フレームに意味ラベルを結び付けます。
    /// </summary>
    [Serializable]
    public sealed class VideoRecordingMarker
    {
        /// <summary>
        /// 目印を打ったフレーム番号です。
        /// </summary>
        public int frame;

        /// <summary>
        /// 目印を打った時刻です。
        /// </summary>
        public float timeSeconds;

        /// <summary>
        /// 目印の説明ラベルです。
        /// </summary>
        public string label;
    }
}
#endif
