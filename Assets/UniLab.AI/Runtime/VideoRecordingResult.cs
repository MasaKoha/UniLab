#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 動画録画の出力結果を表します。
    /// </summary>
    public sealed class VideoRecordingResult
    {
        /// <summary>
        /// 録画名を取得します。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 連番 PNG と manifest の出力先ディレクトリを取得します。
        /// </summary>
        public string OutputDirectory { get; }

        /// <summary>
        /// 書き出したフレーム数を取得します。
        /// </summary>
        public int FrameCount { get; }

        /// <summary>
        /// 録画 FPS を取得します。
        /// </summary>
        public int FramesPerSecond { get; }

        /// <summary>録画した実時間の長さ（秒）。動画の再生時間はこれに一致する。</summary>
        public double DurationSeconds { get; }

        /// <summary>
        /// manifest ファイルパスを取得します。
        /// </summary>
        public string ManifestFilePath { get; }

        /// <summary>
        /// mp4 変換用の ffmpeg コマンドを取得します。
        /// </summary>
        public string FfmpegCommand { get; }

        /// <summary>
        /// 音声を含む録画かどうかを取得します。
        /// </summary>
        public bool HasAudio { get; }

        /// <summary>
        /// 新しい録画結果を初期化します。
        /// </summary>
        public VideoRecordingResult(string name, string outputDirectory, int frameCount, int framesPerSecond, double durationSeconds, string manifestFilePath, string ffmpegCommand, bool hasAudio = false)
        {
            Name = name ?? string.Empty;
            OutputDirectory = outputDirectory ?? string.Empty;
            FrameCount = frameCount;
            FramesPerSecond = framesPerSecond;
            DurationSeconds = durationSeconds;
            ManifestFilePath = manifestFilePath ?? string.Empty;
            FfmpegCommand = ffmpegCommand ?? string.Empty;
            HasAudio = hasAudio;
        }
    }
}
#endif
