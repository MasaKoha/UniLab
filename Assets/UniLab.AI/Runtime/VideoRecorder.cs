#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

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
        private const string AudioFileName = "audio.wav";
        private const int JpegQuality = 90;
        private const int DefaultFramesPerSecond = 30;
        private const int BufferPoolSize = 4;
        private const int BytesPerPixel = 4;
        private const int RenderTextureDepth = 0;
        private const int FirstMipIndex = 0;
        private const int NoRowBytes = 0;
        // AsyncGPUReadback は RenderTexture を左下原点で読み戻すため、JPG へ書く前に行を反転する。
        // 実測で確認済み（反転しないと画面が上下逆さまになる）
        private const bool FlipVerticallyBeforeEncode = true;
        private const double MinimumFrameDuration = 0.0001;
        private const double CaptureIntervalTolerance = 0.9;
        private static readonly WaitForEndOfFrame WaitForEndOfFrameYieldInstruction = new WaitForEndOfFrame();

        private readonly List<VideoRecordingMarker> _markers = new List<VideoRecordingMarker>();
        // 読み戻しや書き出しに失敗し、ファイルが存在しないフレームの番号。
        // frames.txt から除外しないと ffmpeg が存在しないファイルを参照して失敗する
        private readonly HashSet<int> _failedFrameIndexes = new HashSet<int>();
        private readonly object _failedFrameLock = new object();
        private readonly List<double> _frameTimestamps = new List<double>();
        private readonly List<Task> _encodingTasks = new List<Task>();
        private readonly Queue<int> _availableBufferIndexes = new Queue<int>();
        private readonly object _bufferPoolLock = new object();
        private readonly object _encodingTaskLock = new object();

        private AudioRecorder _audioRecorder;
        private NativeArray<byte>[] _buffers;
        private RenderTexture _renderTexture;
        private string _outputDirectory;
        private string _name;
        private string _startedAtRealtime;
        private Coroutine _captureCoroutine;
        private GraphicsFormat _graphicsFormat;
        private int _framesPerSecond;
        private int _frameCount;
        private int _droppedFrameCount;
        private int _failedReadbackCount;
        private int _capturedWidth;
        private int _capturedHeight;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;
        private double _targetFrameInterval;
        private double _recordingStartRealtime;
        private double _lastCaptureRealtime;
        private double _durationSeconds;
        private bool _hasOverriddenFrameRate;
        private bool _isRecording;
        private bool _hasAudio;
        private bool _hasReleasedResources;
        private bool _shouldHideInputOverlayOnStop;
        private bool _inputOverlayEnabled = true;

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
        /// フォレンジックとシナリオ結果が動画上の位置へ辿れるよう、録画中の現在フレームを公開します。
        /// </summary>
        public int FrameCount
        {
            get
            {
                return _frameCount;
            }
        }

        /// <summary>
        /// 合否 JSON が録画負荷の破綻を画像確認なしで読めるようにします。
        /// </summary>
        public int DroppedFrameCount
        {
            get
            {
                return _droppedFrameCount;
            }
        }

        /// <summary>
        /// 録画を開始します。
        /// </summary>
        public static VideoRecorder StartRecording(string outputDirectory, string name, int framesPerSecond = DefaultFramesPerSecond, bool recordAudio = false)
        {
            return StartRecording(outputDirectory, name, framesPerSecond, recordAudio, true);
        }

        /// <summary>
        /// シナリオから録画中オーバーレイを抑制できるようにし、視覚回帰用の静止画汚染を避けます。
        /// </summary>
        public static VideoRecorder StartRecording(string outputDirectory, string name, int framesPerSecond, bool recordAudio, bool inputOverlayEnabled)
        {
            var recorderObject = new GameObject(nameof(VideoRecorder));
            DontDestroyOnLoad(recorderObject);

            var recorder = recorderObject.AddComponent<VideoRecorder>();
            recorder.Initialize(outputDirectory, name, framesPerSecond, recordAudio, inputOverlayEnabled);
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

            StopCaptureLoop();
            _durationSeconds = Time.realtimeSinceStartupAsDouble - _recordingStartRealtime;
            StopAudioRecording();
            RestoreFrameRateSettings();
            WaitForPendingWorkAndReleaseResources();
            HideInputOverlayIfNeeded();

            var result = WriteManifestAndBuildResult();
            UnityEngine.Debug.Log($"[VideoRecorder] 完了: frames={result.FrameCount} duration={_durationSeconds:F2}s dropped={_droppedFrameCount} failedReadback={_failedReadbackCount} output={result.OutputDirectory} ffmpeg={result.FfmpegCommand}");
            Destroy(gameObject);
            return result;
        }

        private void OnDestroy()
        {
            if (_isRecording)
            {
                StopCaptureLoop();
            }

            RestoreFrameRateSettings();
            StopAudioRecording();
            WaitForPendingWorkAndReleaseResources();
            HideInputOverlayIfNeeded();
        }

        private void Initialize(string outputDirectory, string name, int framesPerSecond, bool recordAudio, bool inputOverlayEnabled)
        {
            _outputDirectory = outputDirectory ?? string.Empty;
            _name = string.IsNullOrEmpty(name) ? nameof(VideoRecorder) : name;
            _framesPerSecond = framesPerSecond > 0 ? framesPerSecond : DefaultFramesPerSecond;
            _startedAtRealtime = DateTime.Now.ToString("o");
            _inputOverlayEnabled = inputOverlayEnabled;

            _capturedWidth = Screen.width;
            _capturedHeight = Screen.height;
            _targetFrameInterval = 1.0 / _framesPerSecond;
            _recordingStartRealtime = Time.realtimeSinceStartupAsDouble;
            _lastCaptureRealtime = double.NegativeInfinity;

            Directory.CreateDirectory(_outputDirectory);
            CreateCaptureResources(_capturedWidth, _capturedHeight);
            StartAudioRecordingIfNeeded(recordAudio);
            OverrideFrameRateSettings();
            ShowInputOverlayIfNeeded();

            _isRecording = true;
            _captureCoroutine = StartCoroutine(CaptureFramesCoroutine());
        }

        /// <summary>
        /// 録画中だけ入力可視化を既定で有効にします。
        /// 非録画時に常時出すと静止画系の観測結果を汚すためです。
        /// </summary>
        private void ShowInputOverlayIfNeeded()
        {
            if (!_inputOverlayEnabled)
            {
                _shouldHideInputOverlayOnStop = false;
                return;
            }

            if (InputOverlay.IsVisible)
            {
                _shouldHideInputOverlayOnStop = false;
                return;
            }

            InputOverlay.Show();
            _shouldHideInputOverlayOnStop = true;
        }

        /// <summary>
        /// 録画開始時に自動表示した分だけ停止時に戻します。
        /// 手動表示まで巻き込んで消すと既存利用者の意図を壊すためです。
        /// </summary>
        private void HideInputOverlayIfNeeded()
        {
            if (!_shouldHideInputOverlayOnStop)
            {
                return;
            }

            _shouldHideInputOverlayOnStop = false;
            InputOverlay.Hide();
        }

        private void StartAudioRecordingIfNeeded(bool recordAudio)
        {
            if (!recordAudio)
            {
                return;
            }

            _audioRecorder = new AudioRecorder();
            var audioFilePath = Path.Combine(_outputDirectory, AudioFileName);
            _hasAudio = _audioRecorder.StartRecording(audioFilePath);
            if (_hasAudio)
            {
                return;
            }

            _audioRecorder.Dispose();
            _audioRecorder = null;
            UnityEngine.Debug.LogWarning($"[VideoRecorder] 音声録音を開始できませんでした。 path={audioFilePath}");
        }

        private void CreateCaptureResources(int width, int height)
        {
            var readWrite = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? RenderTextureReadWrite.sRGB
                : RenderTextureReadWrite.Default;
            _renderTexture = new RenderTexture(width, height, RenderTextureDepth, RenderTextureFormat.ARGB32, readWrite);
            _renderTexture.Create();
            _graphicsFormat = _renderTexture.graphicsFormat;

            var bufferLength = width * height * BytesPerPixel;
            _buffers = new NativeArray<byte>[BufferPoolSize];
            for (var bufferIndex = 0; bufferIndex < _buffers.Length; bufferIndex++)
            {
                _buffers[bufferIndex] = new NativeArray<byte>(bufferLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                _availableBufferIndexes.Enqueue(bufferIndex);
            }
        }

        private void OverrideFrameRateSettings()
        {
            _previousTargetFrameRate = Application.targetFrameRate;
            _previousVSyncCount = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _framesPerSecond;
            _hasOverriddenFrameRate = true;
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

        private void StopCaptureLoop()
        {
            _isRecording = false;

            if (_captureCoroutine == null)
            {
                return;
            }

            StopCoroutine(_captureCoroutine);
            _captureCoroutine = null;
        }

        private IEnumerator CaptureFramesCoroutine()
        {
            while (_isRecording)
            {
                yield return WaitForEndOfFrameYieldInstruction;

                PumpAudioFrame();

                var now = Time.realtimeSinceStartupAsDouble;
                if (now - _lastCaptureRealtime < _targetFrameInterval * CaptureIntervalTolerance)
                {
                    continue;
                }

                _lastCaptureRealtime = now;
                CaptureFrame(now - _recordingStartRealtime);
            }
        }

        private void PumpAudioFrame()
        {
            if (_audioRecorder == null || !_audioRecorder.IsRecording)
            {
                return;
            }

            _audioRecorder.PumpFrame();
        }

        private void CaptureFrame(double elapsedSeconds)
        {
            if (!TryTakeAvailableBuffer(out var bufferIndex))
            {
                _droppedFrameCount++;
                return;
            }

            var frameIndex = _frameCount;
            _frameTimestamps.Add(elapsedSeconds);
            _frameCount++;

            ScreenCapture.CaptureScreenshotIntoRenderTexture(_renderTexture);
            AsyncGPUReadback.RequestIntoNativeArray(ref _buffers[bufferIndex], _renderTexture, FirstMipIndex, request =>
            {
                HandleReadbackCompleted(request, bufferIndex, frameIndex);
            });
        }

        private bool TryTakeAvailableBuffer(out int bufferIndex)
        {
            lock (_bufferPoolLock)
            {
                if (_availableBufferIndexes.Count == 0)
                {
                    bufferIndex = -1;
                    return false;
                }

                bufferIndex = _availableBufferIndexes.Dequeue();
                return true;
            }
        }

        private void HandleReadbackCompleted(AsyncGPUReadbackRequest request, int bufferIndex, int frameIndex)
        {
            if (request.hasError)
            {
                _failedReadbackCount++;
                MarkFrameFailed(frameIndex);
                ReturnBuffer(bufferIndex);
                UnityEngine.Debug.LogWarning($"[VideoRecorder] GPU 読み戻しに失敗しました。 frame={frameIndex}");
                return;
            }

            var frameFilePath = Path.Combine(_outputDirectory, string.Format(FrameFileNameFormat, frameIndex));
            var task = Task.Run(() => EncodeAndWriteFrame(bufferIndex, frameFilePath, frameIndex));
            lock (_encodingTaskLock)
            {
                _encodingTasks.Add(task);
            }
        }

        private void EncodeAndWriteFrame(int bufferIndex, string frameFilePath, int frameIndex)
        {
            NativeArray<byte> encodedBytes = default;
            NativeArray<byte> flippedBuffer = default;
            try
            {
                var sourceBuffer = _buffers[bufferIndex];
                if (FlipVerticallyBeforeEncode)
                {
                    flippedBuffer = CreateVerticallyFlippedBuffer(sourceBuffer, _capturedWidth, _capturedHeight);
                    sourceBuffer = flippedBuffer;
                }

                encodedBytes = ImageConversion.EncodeNativeArrayToJPG(sourceBuffer, _graphicsFormat, (uint)_capturedWidth, (uint)_capturedHeight, NoRowBytes, JpegQuality);
                File.WriteAllBytes(frameFilePath, encodedBytes.ToArray());
            }
            catch (Exception exception)
            {
                MarkFrameFailed(frameIndex);
                UnityEngine.Debug.LogWarning($"[VideoRecorder] フレームの書き出しに失敗しました。 frame={frameIndex} {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                if (encodedBytes.IsCreated)
                {
                    encodedBytes.Dispose();
                }

                if (flippedBuffer.IsCreated)
                {
                    flippedBuffer.Dispose();
                }

                ReturnBuffer(bufferIndex);
            }
        }

        private static NativeArray<byte> CreateVerticallyFlippedBuffer(NativeArray<byte> sourceBuffer, int width, int height)
        {
            var rowByteCount = width * BytesPerPixel;
            var flippedBuffer = new NativeArray<byte>(sourceBuffer.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var y = 0; y < height; y++)
            {
                var sourceOffset = y * rowByteCount;
                var destinationOffset = (height - y - 1) * rowByteCount;
                NativeArray<byte>.Copy(sourceBuffer, sourceOffset, flippedBuffer, destinationOffset, rowByteCount);
            }

            return flippedBuffer;
        }

        /// <summary>ファイルが残らなかったフレームを控える。ワーカースレッドからも呼ばれる。</summary>
        private void MarkFrameFailed(int frameIndex)
        {
            lock (_failedFrameLock)
            {
                _failedFrameIndexes.Add(frameIndex);
            }
        }

        private void ReturnBuffer(int bufferIndex)
        {
            lock (_bufferPoolLock)
            {
                _availableBufferIndexes.Enqueue(bufferIndex);
            }
        }

        private void WaitForPendingWorkAndReleaseResources()
        {
            if (_hasReleasedResources)
            {
                return;
            }

            AsyncGPUReadback.WaitAllRequests();
            WaitForEncodingTasks();
            ReleaseCaptureResources();
            _hasReleasedResources = true;
        }

        private void WaitForEncodingTasks()
        {
            Task[] encodingTasks;
            lock (_encodingTaskLock)
            {
                encodingTasks = _encodingTasks.ToArray();
            }

            if (encodingTasks.Length == 0)
            {
                return;
            }

            try
            {
                Task.WaitAll(encodingTasks);
            }
            catch (AggregateException exception)
            {
                UnityEngine.Debug.LogError($"[VideoRecorder] エンコードまたは書き出しに失敗しました。 {exception.Flatten()}");
            }
        }

        private void ReleaseCaptureResources()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_buffers == null)
            {
                return;
            }

            for (var bufferIndex = 0; bufferIndex < _buffers.Length; bufferIndex++)
            {
                if (_buffers[bufferIndex].IsCreated)
                {
                    _buffers[bufferIndex].Dispose();
                }
            }

            _buffers = null;
            lock (_bufferPoolLock)
            {
                _availableBufferIndexes.Clear();
            }
        }

        private void StopAudioRecording()
        {
            if (_audioRecorder == null)
            {
                return;
            }

            try
            {
                _audioRecorder.StopRecording();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[VideoRecorder] 音声録音の停止に失敗しました。 {exception.GetType().Name}: {exception.Message}");
            }
        }

        private VideoRecordingResult WriteManifestAndBuildResult()
        {
            WriteFrameListFile();
            var manifest = CreateManifest(_name, _outputDirectory);
            var manifestFilePath = Path.Combine(_outputDirectory, ManifestFileName);
            var manifestJson = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(manifestFilePath, manifestJson);
            return new VideoRecordingResult(manifest.name, _outputDirectory, _frameCount, _framesPerSecond, _durationSeconds, manifestFilePath, manifest.ffmpegCommand, _hasAudio);
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

            // 失敗したフレームはファイルが無いため除外する。除外分の表示時間は
            // 直前の生き残りフレームへ吸収される（次の生存フレームとの差を取るため自動的にそうなる）
            var survivingFrameIndexes = new List<int>(_frameTimestamps.Count);
            for (var index = 0; index < _frameTimestamps.Count; index++)
            {
                if (!_failedFrameIndexes.Contains(index))
                {
                    survivingFrameIndexes.Add(index);
                }
            }

            if (survivingFrameIndexes.Count == 0)
            {
                return;
            }

            var lineBuilder = new StringBuilder();
            for (var position = 0; position < survivingFrameIndexes.Count; position++)
            {
                var frameIndex = survivingFrameIndexes[position];
                var nextTimestamp = position + 1 < survivingFrameIndexes.Count
                    ? _frameTimestamps[survivingFrameIndexes[position + 1]]
                    : _durationSeconds;
                var frameDuration = Math.Max(nextTimestamp - _frameTimestamps[frameIndex], MinimumFrameDuration);

                lineBuilder.Append("file '").Append(string.Format(FrameFileNameFormat, frameIndex)).Append("'\n");
                lineBuilder.Append("duration ").Append(frameDuration.ToString("F6", CultureInfo.InvariantCulture)).Append('\n');
            }

            lineBuilder.Append("file '").Append(string.Format(FrameFileNameFormat, survivingFrameIndexes[survivingFrameIndexes.Count - 1])).Append("'\n");

            File.WriteAllText(Path.Combine(_outputDirectory, FrameListFileName), lineBuilder.ToString());
        }

        private VideoRecordingResult BuildResult()
        {
            var manifestFilePath = Path.Combine(_outputDirectory ?? string.Empty, ManifestFileName);
            var ffmpegCommand = CreateFfmpegCommand(_framesPerSecond, _outputDirectory ?? string.Empty, _name ?? string.Empty, _durationSeconds, _hasAudio);
            return new VideoRecordingResult(_name, _outputDirectory, _frameCount, _framesPerSecond, _durationSeconds, manifestFilePath, ffmpegCommand, _hasAudio);
        }

        private VideoRecordingManifest CreateManifest(string name, string outputDirectory)
        {
            return new VideoRecordingManifest
            {
                name = name,
                framesPerSecond = _framesPerSecond,
                frameCount = _frameCount,
                droppedFrameCount = _droppedFrameCount,
                durationSeconds = (float)_durationSeconds,
                width = _capturedWidth,
                height = _capturedHeight,
                hasAudio = _hasAudio,
                audioSampleRate = _audioRecorder != null && _hasAudio ? _audioRecorder.SampleRate : 0,
                audioChannelCount = _audioRecorder != null && _hasAudio ? _audioRecorder.ChannelCount : 0,
                inputOverlay = _inputOverlayEnabled,
                startedAtRealtime = _startedAtRealtime,
                ffmpegCommand = CreateFfmpegCommand(_framesPerSecond, outputDirectory, name, _durationSeconds, _hasAudio),
                markers = _markers.ToArray(),
            };
        }

        /// <summary>連番 JPG を mp4 へ変換する ffmpeg コマンドを組み立てる。変換の実行は呼び出し側が行う。</summary>
        public static string CreateFfmpegCommand(int framesPerSecond, string outputDirectory, string name, double durationSeconds, bool hasAudio = false)
        {
            var frameListFilePath = Path.Combine(outputDirectory, FrameListFileName);
            var outputFilePath = Path.Combine(outputDirectory, $"{name}.mp4");
            var durationArgument = durationSeconds.ToString("F6", CultureInfo.InvariantCulture);

            if (hasAudio)
            {
                var audioFilePath = Path.Combine(outputDirectory, AudioFileName);
                return $"ffmpeg -y -f concat -safe 0 -i \"{frameListFilePath}\" -i \"{audioFilePath}\" -r {framesPerSecond} -t {durationArgument} -c:v libx264 -pix_fmt yuv420p -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\" -c:a aac -shortest \"{outputFilePath}\"";
            }

            return $"ffmpeg -y -f concat -safe 0 -i \"{frameListFilePath}\" -r {framesPerSecond} -t {durationArgument} -c:v libx264 -pix_fmt yuv420p -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\" \"{outputFilePath}\"";
        }
    }
}
#endif
