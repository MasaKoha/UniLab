using System;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// Unity ログをファイルへ複写する。Boot 等で1回だけ生成して Initialize を呼ぶ。
    /// </summary>
    public sealed class FileLogSink : IDisposable
    {
        private const string FileNamePrefix = "player-log-";
        private const string FileNameTimestampFormat = "yyyyMMdd-HHmmss";
        private const string FileExtension = ".log";
        private const string LineTimestampFormat = "HH:mm:ss.fff";

        private readonly object _writeLock = new object();
        private readonly string _outputDirectory;

        private StreamWriter _streamWriter;
        private bool _isInitialized;
        private bool _isDisposed;
        private bool _hasReportedFailure;

        /// <summary>
        /// 出力先ディレクトリを指定して生成する。null または空なら DebugOutputPath の既定を使う。
        /// </summary>
        public FileLogSink(string outputDirectory = null)
        {
            _outputDirectory = string.IsNullOrEmpty(outputDirectory) ? DebugOutputPath.DirectoryPath : outputDirectory;
            var fileName = $"{FileNamePrefix}{DateTime.Now.ToString(FileNameTimestampFormat)}{FileExtension}";
            OutputFilePath = Path.Combine(_outputDirectory, fileName);
        }

        /// <summary>
        /// 出力先のファイルパス。
        /// </summary>
        public string OutputFilePath { get; }

        /// <summary>
        /// ログの購読とファイルの初期化を行う。二重呼び出しは無視する。
        /// </summary>
        public void Initialize()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isInitialized)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_outputDirectory);
                _streamWriter = new StreamWriter(OutputFilePath, false, Encoding.UTF8);
                Application.logMessageReceivedThreaded += HandleLogMessageReceivedThreaded;
                _isInitialized = true;
            }
            catch (Exception exception)
            {
                StopLoggingBecauseOfFailure(exception);
            }
        }

        /// <summary>
        /// 購読解除とファイルハンドルの破棄を行います。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (_isInitialized)
            {
                Application.logMessageReceivedThreaded -= HandleLogMessageReceivedThreaded;
                _isInitialized = false;
            }

            lock (_writeLock)
            {
                _streamWriter?.Dispose();
                _streamWriter = null;
            }
        }

        private void HandleLogMessageReceivedThreaded(string condition, string stackTrace, LogType logType)
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                lock (_writeLock)
                {
                    if (_streamWriter == null)
                    {
                        return;
                    }

                    var timestamp = DateTime.Now.ToString(LineTimestampFormat);
                    _streamWriter.Write('[');
                    _streamWriter.Write(timestamp);
                    _streamWriter.Write("][");
                    _streamWriter.Write(logType);
                    _streamWriter.Write("] ");
                    _streamWriter.WriteLine(condition);

                    if (logType == LogType.Error || logType == LogType.Exception)
                    {
                        _streamWriter.WriteLine(stackTrace);
                    }

                    // perf: クラッシュ直前のログ欠落を避けることを優先して毎回 Flush する。
                    _streamWriter.Flush();
                }
            }
            catch (Exception exception)
            {
                StopLoggingBecauseOfFailure(exception);
            }
        }

        private void StopLoggingBecauseOfFailure(Exception exception)
        {
            lock (_writeLock)
            {
                if (_isInitialized)
                {
                    Application.logMessageReceivedThreaded -= HandleLogMessageReceivedThreaded;
                    _isInitialized = false;
                }

                _streamWriter?.Dispose();
                _streamWriter = null;

                if (_hasReportedFailure)
                {
                    return;
                }

                _hasReportedFailure = true;
                UnityEngine.Debug.LogWarning($"FileLogSink はファイル出力失敗のため停止しました。 path={OutputFilePath}, reason={exception.Message}");
            }
        }
    }
}
#endif
