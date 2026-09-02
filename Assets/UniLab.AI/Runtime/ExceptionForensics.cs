#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.AI
{
    /// <summary>
    /// 例外・エラーログの瞬間の状況を自動保存する。
    /// Boot で生成できない環境でもランナーから開始できるよう、寿命を明示管理します。
    /// </summary>
    public sealed class ExceptionForensics : IDisposable
    {
        private const string ForensicsDirectoryName = "forensics";
        private const string TimestampFormat = "yyyyMMdd-HHmmss";
        private const string ErrorFileName = "error.txt";
        private const string RecentLogFileName = "recent-log.txt";
        private const string ContextFileName = "context.json";
        private const string SnapshotFileName = "snapshot.json";
        private const string ScreenshotFileName = "screenshot.png";
        private const string HierarchyFileName = "hierarchy.json";
        private const int RecentLogLimit = 200;

        private static ExceptionForensics _current;

        private readonly Queue<ForensicsPendingLog> _pendingLogs = new Queue<ForensicsPendingLog>();
        private readonly HashSet<string> _capturedStacks = new HashSet<string>();
        private readonly Dictionary<string, string> _directoryByStack = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _repeatCountByStack = new Dictionary<string, int>();
        private readonly HashSet<string> _repeatStacksToRewrite = new HashSet<string>();
        private readonly List<string> _capturedDirectories = new List<string>();
        private readonly List<string> _recentLogs = new List<string>();
        private readonly object _lockObject = new object();

        private ExceptionForensicsDriver _driver;
        private string _outputRootDirectory;
        private int _maxCaptureCount;
        private int _sequence;
        private int _suppressedCount;
        private bool _isDisposed;
        private bool _isCapturing;

        /// <summary>
        /// ランナーが結果 JSON に転記できるよう、現在有効なインスタンスを公開します。
        /// </summary>
        public static ExceptionForensics Current
        {
            get
            {
                return _current;
            }
        }

        /// <summary>
        /// このラン中に保存した件数です。
        /// </summary>
        public int CapturedCount
        {
            get
            {
                return _capturedDirectories.Count;
            }
        }

        /// <summary>
        /// 洪水抑制や重複抑制で保存しなかった件数です。
        /// </summary>
        public int SuppressedCount
        {
            get
            {
                return _suppressedCount;
            }
        }

        /// <summary>
        /// 保存したフォレンジックディレクトリです。
        /// </summary>
        public string[] CapturedDirectories
        {
            get
            {
                return _capturedDirectories.ToArray();
            }
        }

        /// <summary>
        /// 例外購読とメインスレッド収集用ドライバを開始します。
        /// </summary>
        public void Initialize(string outputRootDirectory = null, int maxCaptureCount = 20)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ExceptionForensics));
            }

            _outputRootDirectory = string.IsNullOrEmpty(outputRootDirectory)
                ? Path.Combine(DebugOutputPath.DirectoryPath, ForensicsDirectoryName)
                : outputRootDirectory;
            _maxCaptureCount = maxCaptureCount > 0 ? maxCaptureCount : 20;
            Directory.CreateDirectory(_outputRootDirectory);

            if (_driver == null)
            {
                var driverObject = new GameObject(nameof(ExceptionForensics));
                UnityEngine.Object.DontDestroyOnLoad(driverObject);
                _driver = driverObject.AddComponent<ExceptionForensicsDriver>();
                _driver.Initialize(this);
            }

            _current = this;
            Application.logMessageReceivedThreaded += HandleLogMessageReceivedThreaded;
        }

        /// <summary>
        /// 購読とドライバを必ず破棄し、Play セッションをまたぐ重複保存を防ぎます。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Application.logMessageReceivedThreaded -= HandleLogMessageReceivedThreaded;
            if (_driver != null)
            {
                _driver.Clear();
                UnityEngine.Object.Destroy(_driver.gameObject);
                _driver = null;
            }

            if (_current == this)
            {
                _current = null;
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        internal void CapturePending()
        {
            if (_isCapturing)
            {
                return;
            }

            while (TryDequeue(out var pendingLog))
            {
                Capture(pendingLog);
            }

            RewriteRepeatContexts();
        }

        private void HandleLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            AppendRecentLog(condition, type);
            if (type != LogType.Exception && type != LogType.Error)
            {
                return;
            }

            if (_isCapturing)
            {
                return;
            }

            var stackKey = string.IsNullOrEmpty(stackTrace) ? condition ?? string.Empty : stackTrace;
            lock (_lockObject)
            {
                if (_capturedStacks.Contains(stackKey))
                {
                    _suppressedCount++;
                    if (_repeatCountByStack.ContainsKey(stackKey))
                    {
                        _repeatCountByStack[stackKey]++;
                    }

                    _repeatStacksToRewrite.Add(stackKey);
                    return;
                }

                if (_capturedDirectories.Count >= _maxCaptureCount)
                {
                    _suppressedCount++;
                    return;
                }

                _capturedStacks.Add(stackKey);
                _pendingLogs.Enqueue(new ForensicsPendingLog(condition, stackTrace, type, stackKey));
            }
        }

        private bool TryDequeue(out ForensicsPendingLog pendingLog)
        {
            lock (_lockObject)
            {
                if (_pendingLogs.Count == 0)
                {
                    pendingLog = null;
                    return false;
                }

                pendingLog = _pendingLogs.Dequeue();
                return true;
            }
        }

        private void Capture(ForensicsPendingLog pendingLog)
        {
            _isCapturing = true;
            try
            {
                _sequence++;
                var directoryName = $"{DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture)}-{_sequence}";
                var outputDirectory = Path.Combine(_outputRootDirectory, directoryName);
                Directory.CreateDirectory(outputDirectory);

                File.WriteAllText(Path.Combine(outputDirectory, ErrorFileName), BuildErrorText(pendingLog));
                File.WriteAllText(Path.Combine(outputDirectory, RecentLogFileName), BuildRecentLogText());
                WriteContext(outputDirectory, 0);
                var snapshot = UiSnapshot.Capture();
                File.WriteAllText(Path.Combine(outputDirectory, SnapshotFileName), JsonUtility.ToJson(snapshot, true));
                File.WriteAllText(Path.Combine(outputDirectory, HierarchyFileName), JsonUtility.ToJson(SceneHierarchyDumper.Dump(), true));
                ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, ScreenshotFileName));

                _directoryByStack[pendingLog.StackKey] = outputDirectory;
                _repeatCountByStack[pendingLog.StackKey] = 0;
                _capturedDirectories.Add(outputDirectory);
            }
            finally
            {
                _isCapturing = false;
            }
        }

        private void RewriteRepeatContexts()
        {
            string[] stackKeys;
            lock (_lockObject)
            {
                if (_repeatStacksToRewrite.Count == 0)
                {
                    return;
                }

                stackKeys = new string[_repeatStacksToRewrite.Count];
                _repeatStacksToRewrite.CopyTo(stackKeys);
                _repeatStacksToRewrite.Clear();
            }

            for (var stackIndex = 0; stackIndex < stackKeys.Length; stackIndex++)
            {
                RewriteRepeatContext(stackKeys[stackIndex]);
            }
        }

        private void RewriteRepeatContext(string stackKey)
        {
            if (!_directoryByStack.TryGetValue(stackKey, out var outputDirectory))
            {
                return;
            }

            var repeatCount = _repeatCountByStack.TryGetValue(stackKey, out var count) ? count : 0;
            WriteContext(outputDirectory, repeatCount);
        }

        private static string BuildErrorText(ForensicsPendingLog pendingLog)
        {
            return $"type: {pendingLog.Type}\nmessage:\n{pendingLog.Condition}\n\nstackTrace:\n{pendingLog.StackTrace}\n";
        }

        private void AppendRecentLog(string condition, LogType type)
        {
            lock (_lockObject)
            {
                _recentLogs.Add($"{DateTime.Now.ToString("o", CultureInfo.InvariantCulture)} [{type}] {condition}");
                while (_recentLogs.Count > RecentLogLimit)
                {
                    _recentLogs.RemoveAt(0);
                }
            }
        }

        private string BuildRecentLogText()
        {
            lock (_lockObject)
            {
                return string.Join("\n", _recentLogs);
            }
        }

        private static void WriteContext(string outputDirectory, int repeatCount)
        {
            var context = new ForensicsContextSnapshot
            {
                frame = Time.frameCount,
                capturedNextFrame = true,
                realtimeSinceStartup = Time.realtimeSinceStartup,
                activeScene = SceneManager.GetActiveScene().name,
                scenario = ForensicsContext.ScenarioName,
                stepIndex = ForensicsContext.StepIndex,
                lastAction = ForensicsContext.LastAction,
                recordingName = ForensicsContext.RecordingName,
                recordingFrame = ForensicsContext.RecordingFrame,
                repeatCount = repeatCount,
            };
            File.WriteAllText(Path.Combine(outputDirectory, ContextFileName), JsonUtility.ToJson(context, true));
        }
    }
}
#endif
