#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>稼働中のファイルログを優先し、未設定時は直近の Unity ログを返します。</summary>
    internal sealed class AiConsoleLog : IDisposable
    {
        private const int RingCapacity = 200;
        private readonly Queue<string> _lines = new Queue<string>(RingCapacity);
        // perf: 繰り返す観測の本文バッファを再利用する。
        private readonly StringBuilder _builder = new StringBuilder();

        internal AiConsoleLog()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        internal string Read(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _builder.Clear();
            if (count == 0)
            {
                return string.Empty;
            }

            var path = FileLogSink.ActiveOutputFilePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return ReadFile(path, count);
            }

            var skipCount = Math.Max(0, _lines.Count - count);
            foreach (var line in _lines)
            {
                if (skipCount-- > 0)
                {
                    continue;
                }

                _builder.AppendLine(line);
            }

            return _builder.ToString().TrimEnd();
        }

        /// <summary>ドメイン再読み込みなしの Play 再開でも二重購読を防ぎます。</summary>
        public void Dispose()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private string ReadFile(string path, int count)
        {
            var tail = new Queue<string>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    tail.Enqueue(line);
                    if (tail.Count > count)
                    {
                        tail.Dequeue();
                    }
                }
            }

            foreach (var line in tail)
            {
                _builder.AppendLine(line);
            }

            return _builder.ToString().TrimEnd();
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            using (var reader = new StringReader($"[{type}] {condition}\n{stackTrace}".TrimEnd()))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (_lines.Count == RingCapacity)
                    {
                        _lines.Dequeue();
                    }

                    _lines.Enqueue(line);
                }
            }
        }
    }
}
#endif
