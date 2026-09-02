using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 連番 JPG による画面録画を行う使い捨てコンポーネントです。
    /// </summary>
    public sealed class VideoRecorder : MonoBehaviour
    {
        /// <summary>録画結果に添える manifest のファイル名。呼び出し側が参照するために公開する。</summary>
        public const string ManifestFileName = "recording-manifest.json";

        /// <summary>ffmpeg の concat デマルチプレクサへ渡すフレーム一覧のファイル名。</summary>
        public const string FrameListFileName = "frames.txt";

        private const string FrameFileNameFormat = "frame-{0:D5}.jpg";
        // 検証で UI のテキストを読む用途のため、圧縮ノイズで文字が潰れない範囲の品質を選ぶ。
        private const int JpegQuality = 90;
        private const int DefaultFramesPerSecond = 30;
        // concat が 0 秒フレームを弾くため、下限を置く
        private const double MinimumFrameDuration = 0.0001;
        // 描画レートと間引き周期が一致したときの取りこぼしを避けるための許容係数
        private const double CaptureIntervalTolerance = 0.9;
        private static readonly WaitForEndOfFrame WaitForEndOfFrameYieldInstruction = new WaitForEndOfFrame();

        private readonly List<VideoRecordingMarker> _markers = new List<VideoRecordingMarker>();
        // 各フレームを撮った実時刻（録画開始からの経過秒）。動画の尺を実時間へ一致させるために使う
        private readonly List<double> _frameTimestamps = new List<double>();

        private string _outputDirectory;
        private string _name;
        private string _startedAtRealtime;
        private Coroutine _captureCoroutine;
        private int _framesPerSecond;
        private int _frameCount;
        private double _targetFrameInterval;
        private double _recordingStartRealtime;
        private double _lastCaptureRealtime;
        private double _durationSeconds;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;
        private bool _hasOverriddenFrameRate;
        private int _capturedWidth;
        private int _capturedHeight;
        private bool _hasCapturedFrameSize;
        private bool _isRecording;

        /// <summary>
        /// 録画中かどうかを取得します。
        /// </summary>
        public bool IsRecording
        {
            get
            {
                return _isRecording;
            }
        }

        /// <summary>
        /// 録画を開始します。
        /// </summary>
        public static VideoRecorder StartRecording(string outputDirectory, string name, int framesPerSecond = DefaultFramesPerSecond)
        {
            var recorderObject = new GameObject(nameof(VideoRecorder));
            DontDestroyOnLoad(recorderObject);

            var recorder = recorderObject.AddComponent<VideoRecorder>();
            recorder.Initialize(outputDirectory, name, framesPerSecond);
            return recorder;
        }

        /// <summary>
        /// 現在フレームに目印を追加します。
        /// </summary>
        public void AddMarker(string label)
        {
            if (!_isRecording)
            {
                return;
            }

            _markers.Add(new VideoRecordingMarker
            {
                frame = _frameCount,
                timeSeconds = (float)(Time.realtimeSinceStartupAsDouble - _recordingStartRealtime),
                label = label ?? string.Empty,
            });
        }

        /// <summary>
        /// 録画を停止し、manifest を書き出した結果を返します。
        /// </summary>
        public VideoRecordingResult StopRecording()
        {
            if (!_isRecording)
            {
                return BuildResult();
            }

            _isRecording = false;

            if (_captureCoroutine != null)
            {
                StopCoroutine(_captureCoroutine);
                _captureCoroutine = null;
            }

            RestoreFrameRateSettings();
            _durationSeconds = Time.realtimeSinceStartupAsDouble - _recordingStartRealtime;

            var result = WriteManifestAndBuildResult();
            UnityEngine.Debug.Log($"[VideoRecorder] 完了: frames={result.FrameCount} duration={_durationSeconds:F2}s output={result.OutputDirectory} ffmpeg={result.FfmpegCommand}");
            Destroy(gameObject);
            return result;
        }

        private void OnDestroy()
        {
            RestoreFrameRateSettings();
        }

        /// <summary>録画のために絞った描画レート設定を元へ戻す。二重呼び出しに耐える。</summary>
        private void RestoreFrameRateSettings()
        {
            if (!_hasOverriddenFrameRate)
            {
                return;
            }

            _hasOverriddenFrameRate = false;
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
        }

        private void Initialize(string outputDirectory, string name, int framesPerSecond)
        {
            _outputDirectory = outputDirectory ?? string.Empty;
            _name = string.IsNullOrEmpty(name) ? nameof(VideoRecorder) : name;
            _framesPerSecond = framesPerSecond > 0 ? framesPerSecond : DefaultFramesPerSecond;
            _startedAtRealtime = DateTime.Now.ToString("o");

            _targetFrameInterval = 1.0 / _framesPerSecond;
            _recordingStartRealtime = Time.realtimeSinceStartupAsDouble;
            _lastCaptureRealtime = double.NegativeInfinity;

            Directory.CreateDirectory(_outputDirectory);

            // Time.captureFramerate は設定しない。設定するとゲーム時間が固定ステップで進み、
            // 動画の尺が実時間から乖離する（音声を重ねる際に同期できなくなる）。
            // 代わりに実際の描画レートを目標 fps へ絞る。こうすると実時間・ゲーム時間・動画の尺が
            // すべて一致し、プレイヤーが見る速度そのものが録れる
            _previousTargetFrameRate = Application.targetFrameRate;
            _previousVSyncCount = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _framesPerSecond;
            _hasOverriddenFrameRate = true;

            _isRecording = true;
            _captureCoroutine = StartCoroutine(CaptureFramesCoroutine());
        }

        private IEnumerator CaptureFramesCoroutine()
        {
            while (_isRecording)
            {
                yield return WaitForEndOfFrameYieldInstruction;

                // 実時間で間引く。ゲームが目標 fps より速く回っても撮りすぎず、
                // 遅れても実時刻を記録しているので尺は狂わない。
                // 判定間隔を目標そのものにすると、描画レートと周期が一致したときに
                // わずかなゆらぎで1フレームおきに取りこぼす。許容係数で余裕を持たせる
                var now = Time.realtimeSinceStartupAsDouble;
                if (now - _lastCaptureRealtime < _targetFrameInterval * CaptureIntervalTolerance)
                {
                    continue;
                }

                _lastCaptureRealtime = now;
                CaptureFrame(now - _recordingStartRealtime);
            }
        }

        private void CaptureFrame(double elapsedSeconds)
        {
            Texture2D screenshotTexture = null;
            try
            {
                screenshotTexture = ScreenCapture.CaptureScreenshotAsTexture();
                if (!_hasCapturedFrameSize)
                {
                    _capturedWidth = screenshotTexture.width;
                    _capturedHeight = screenshotTexture.height;
                    _hasCapturedFrameSize = true;
                }

                var frameBytes = screenshotTexture.EncodeToJPG(JpegQuality);
                var frameFilePath = Path.Combine(_outputDirectory, string.Format(FrameFileNameFormat, _frameCount));
                File.WriteAllBytes(frameFilePath, frameBytes);
                _frameTimestamps.Add(elapsedSeconds);
                _frameCount++;
            }
            finally
            {
                if (screenshotTexture != null)
                {
                    // perf: 録画は低頻度の操作であり、常駐バッファを持つより都度破棄のほうが害が小さい。
                    Destroy(screenshotTexture);
                }
            }
        }

        private VideoRecordingResult WriteManifestAndBuildResult()
        {
            WriteFrameListFile();
            var manifest = CreateManifest(_name, _outputDirectory);
            var manifestFilePath = Path.Combine(_outputDirectory, ManifestFileName);
            var manifestJson = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(manifestFilePath, manifestJson);
            return new VideoRecordingResult(manifest.name, _outputDirectory, _frameCount, _framesPerSecond, _durationSeconds, manifestFilePath, manifest.ffmpegCommand);
        }

        /// <summary>
        /// ffmpeg の concat デマルチプレクサ用のフレーム一覧を書き出す。
        /// 各フレームに実測の表示時間を持たせることで、動画の尺が録画した実時間と一致する。
        /// ファイル名は相対で書く（録画後にディレクトリを移動しても壊れないため）。
        /// </summary>
        private void WriteFrameListFile()
        {
            if (_frameTimestamps.Count == 0)
            {
                return;
            }

            var lineBuilder = new StringBuilder();
            for (var index = 0; index < _frameTimestamps.Count; index++)
            {
                var frameFileName = string.Format(FrameFileNameFormat, index);
                var nextTimestamp = index + 1 < _frameTimestamps.Count
                    ? _frameTimestamps[index + 1]
                    : _durationSeconds;
                var frameDuration = Math.Max(nextTimestamp - _frameTimestamps[index], MinimumFrameDuration);

                lineBuilder.Append("file '").Append(frameFileName).Append("'\n");
                lineBuilder.Append("duration ").Append(frameDuration.ToString("F6", CultureInfo.InvariantCulture)).Append('\n');
            }

            // concat デマルチプレクサは最後の duration を無視するため、末尾のファイルをもう一度並べる
            lineBuilder.Append("file '").Append(string.Format(FrameFileNameFormat, _frameTimestamps.Count - 1)).Append("'\n");

            File.WriteAllText(Path.Combine(_outputDirectory, FrameListFileName), lineBuilder.ToString());
        }

        private VideoRecordingResult BuildResult()
        {
            var manifestFilePath = Path.Combine(_outputDirectory ?? string.Empty, ManifestFileName);
            var ffmpegCommand = CreateFfmpegCommand(_framesPerSecond, _outputDirectory ?? string.Empty, _name ?? string.Empty, _durationSeconds);
            return new VideoRecordingResult(_name, _outputDirectory, _frameCount, _framesPerSecond, _durationSeconds, manifestFilePath, ffmpegCommand);
        }

        private VideoRecordingManifest CreateManifest(string name, string outputDirectory)
        {
            return new VideoRecordingManifest
            {
                name = name,
                framesPerSecond = _framesPerSecond,
                frameCount = _frameCount,
                durationSeconds = (float)_durationSeconds,
                width = _capturedWidth,
                height = _capturedHeight,
                startedAtRealtime = _startedAtRealtime,
                ffmpegCommand = CreateFfmpegCommand(_framesPerSecond, outputDirectory, name, _durationSeconds),
                markers = _markers.ToArray(),
            };
        }

        /// <summary>連番 JPG を mp4 へ変換する ffmpeg コマンドを組み立てる。変換の実行は呼び出し側が行う。</summary>
        public static string CreateFfmpegCommand(int framesPerSecond, string outputDirectory, string name, double durationSeconds)
        {
            var frameListFilePath = Path.Combine(outputDirectory, FrameListFileName);
            var outputFilePath = Path.Combine(outputDirectory, $"{name}.mp4");
            var durationArgument = durationSeconds.ToString("F6", CultureInfo.InvariantCulture);

            // concat デマルチプレクサでフレームごとの実測表示時間を反映し、-r で一定フレームレートへ均す。
            // -t で尺を実測値に固定する。これが無いと concat の末尾処理と丸めで数フレーム伸び、
            // 実測では 7.63 秒の録画が 7.70 秒の動画になった（-t 付きなら誤差 1 ミリ秒未満）
            return $"ffmpeg -y -f concat -safe 0 -i \"{frameListFilePath}\" -r {framesPerSecond} -t {durationArgument} -c:v libx264 -pix_fmt yuv420p -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\" \"{outputFilePath}\"";
        }
    }
}
#endif
