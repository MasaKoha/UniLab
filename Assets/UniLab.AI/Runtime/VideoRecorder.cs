using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 連番 PNG による画面録画を行う使い捨てコンポーネントです。
    /// </summary>
    public sealed class VideoRecorder : MonoBehaviour
    {
        /// <summary>録画結果に添える manifest のファイル名。呼び出し側が参照するために公開する。</summary>
        public const string ManifestFileName = "recording-manifest.json";

        private const string FrameFileNameFormat = "frame-{0:D5}.png";
        private const string FfmpegInputPattern = "frame-%05d.png";
        private const int DefaultFramesPerSecond = 30;
        private static readonly WaitForEndOfFrame WaitForEndOfFrameYieldInstruction = new WaitForEndOfFrame();

        private readonly List<VideoRecordingMarker> _markers = new List<VideoRecordingMarker>();

        private string _outputDirectory;
        private string _name;
        private string _startedAtRealtime;
        private Coroutine _captureCoroutine;
        private int _framesPerSecond;
        private int _frameCount;
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
                timeSeconds = (float)_frameCount / _framesPerSecond,
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

            Time.captureFramerate = 0;

            var result = WriteManifestAndBuildResult();
            UnityEngine.Debug.Log($"[VideoRecorder] 完了: frames={result.FrameCount} output={result.OutputDirectory} ffmpeg={result.FfmpegCommand}");
            Destroy(gameObject);
            return result;
        }

        private void OnDestroy()
        {
            Time.captureFramerate = 0;
        }

        private void Initialize(string outputDirectory, string name, int framesPerSecond)
        {
            _outputDirectory = outputDirectory ?? string.Empty;
            _name = string.IsNullOrEmpty(name) ? nameof(VideoRecorder) : name;
            _framesPerSecond = framesPerSecond > 0 ? framesPerSecond : DefaultFramesPerSecond;
            _startedAtRealtime = DateTime.Now.ToString("o");

            Directory.CreateDirectory(_outputDirectory);

            Time.captureFramerate = _framesPerSecond;
            _isRecording = true;
            _captureCoroutine = StartCoroutine(CaptureFramesCoroutine());
        }

        private IEnumerator CaptureFramesCoroutine()
        {
            while (_isRecording)
            {
                yield return WaitForEndOfFrameYieldInstruction;
                CaptureFrame();
            }
        }

        private void CaptureFrame()
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

                var pngBytes = screenshotTexture.EncodeToPNG();
                var frameFilePath = Path.Combine(_outputDirectory, string.Format(FrameFileNameFormat, _frameCount));
                File.WriteAllBytes(frameFilePath, pngBytes);
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
            var manifest = CreateManifest(_name, _outputDirectory);
            var manifestFilePath = Path.Combine(_outputDirectory, ManifestFileName);
            var manifestJson = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(manifestFilePath, manifestJson);
            return new VideoRecordingResult(manifest.name, _outputDirectory, _frameCount, _framesPerSecond, manifestFilePath, manifest.ffmpegCommand);
        }

        private VideoRecordingResult BuildResult()
        {
            var manifestFilePath = Path.Combine(_outputDirectory ?? string.Empty, ManifestFileName);
            var ffmpegCommand = CreateFfmpegCommand(_framesPerSecond, _outputDirectory ?? string.Empty, _name ?? string.Empty);
            return new VideoRecordingResult(_name, _outputDirectory, _frameCount, _framesPerSecond, manifestFilePath, ffmpegCommand);
        }

        private VideoRecordingManifest CreateManifest(string name, string outputDirectory)
        {
            return new VideoRecordingManifest
            {
                name = name,
                framesPerSecond = _framesPerSecond,
                frameCount = _frameCount,
                width = _capturedWidth,
                height = _capturedHeight,
                startedAtRealtime = _startedAtRealtime,
                ffmpegCommand = CreateFfmpegCommand(_framesPerSecond, outputDirectory, name),
                markers = _markers.ToArray(),
            };
        }

        /// <summary>連番 PNG を mp4 へ変換する ffmpeg コマンドを組み立てる。変換の実行は呼び出し側が行う。</summary>
        public static string CreateFfmpegCommand(int framesPerSecond, string outputDirectory, string name)
        {
            var inputFilePath = Path.Combine(outputDirectory, FfmpegInputPattern);
            var outputFilePath = Path.Combine(outputDirectory, $"{name}.mp4");
            return $"ffmpeg -y -framerate {framesPerSecond} -i \"{inputFilePath}\" -c:v libx264 -pix_fmt yuv420p -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\" \"{outputFilePath}\"";
        }
    }
}
#endif
