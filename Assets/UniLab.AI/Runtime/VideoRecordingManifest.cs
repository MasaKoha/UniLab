using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 録画のフレーム列とマーカーを復元するための manifest です。
    /// </summary>
    [Serializable]
    public sealed class VideoRecordingManifest
    {
        /// <summary>
        /// 録画名です。
        /// </summary>
        public string name;

        /// <summary>
        /// 録画 FPS です。
        /// </summary>
        public int framesPerSecond;

        /// <summary>
        /// 書き出した総フレーム数です。
        /// </summary>
        public int frameCount;

        /// <summary>
        /// 録画幅です。
        /// </summary>
        public int width;

        /// <summary>
        /// 録画高さです。
        /// </summary>
        public int height;

        /// <summary>
        /// 録画開始時刻です。
        /// </summary>
        public string startedAtRealtime;

        /// <summary>
        /// mp4 変換用の ffmpeg コマンドです。
        /// </summary>
        public string ffmpegCommand;

        /// <summary>
        /// 動画上の意味的な目印一覧です。
        /// </summary>
        public VideoRecordingMarker[] markers;
    }
}
#endif
