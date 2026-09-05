#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>ファイルのフラッシュ時点に依存せず、直近の Unity ログを行単位で保持します。</summary>
    internal sealed class AiConsoleLog : IDisposable
    {
        private const int RingCapacity = 500;
        private const int MaximumStackTraceLines = 3;
        private readonly Queue<string> _lines = new Queue<string>(RingCapacity);
        private readonly Queue<bool> _errorFlags = new Queue<bool>(RingCapacity);
        // perf: 繰り返す観測の本文バッファを再利用する。
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>Play 開始時にログ購読を開始します。</summary>
        internal AiConsoleLog()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        /// <summary>対象レベルで絞り込んだ末尾の行を返します。</summary>
        internal string Read(int count, string level = "all")
        {
            Validate(count, level);
            _builder.Clear();
            var matchingCount = level == "all" ? _lines.Count : CountErrorLines();
            var skipCount = Math.Max(0, matchingCount - count);
            using (var flags = _errorFlags.GetEnumerator())
            {
                foreach (var line in _lines)
                {
                    flags.MoveNext();
                    if (level == "error" && !flags.Current)
                    {
                        continue;
                    }

                    if (skipCount-- > 0)
                    {
                        continue;
                    }

                    _builder.AppendLine(line);
                }
            }

            return _builder.ToString().TrimEnd();
        }

        /// <summary>未購読の Editor 時も同じ引数契約で検証します。</summary>
        internal static void Validate(int count, string level)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (level != "all" && level != "error")
            {
                throw new ArgumentException("level は all または error を指定してください。", nameof(level));
            }
        }

        /// <summary>ドメイン再読み込みなしの Play 再開でも二重購読を防ぎます。</summary>
        public void Dispose()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        /// <summary>本文とエラー系の先頭 3 行のスタックを同じレベルで保持します。</summary>
        internal void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            var isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            AppendLines($"[{type}] {condition}", isError, int.MaxValue);
            if (isError && !string.IsNullOrEmpty(stackTrace))
            {
                AppendLines(stackTrace, true, MaximumStackTraceLines);
            }
        }

        private void AppendLines(string text, bool isError, int maximumLines)
        {
            using (var reader = new StringReader(text))
            {
                for (var lineIndex = 0; lineIndex < maximumLines; lineIndex++)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    AppendLine(line, isError);
                }
            }
        }

        private void AppendLine(string line, bool isError)
        {
            if (_lines.Count == RingCapacity)
            {
                _lines.Dequeue();
                _errorFlags.Dequeue();
            }

            _lines.Enqueue(line);
            _errorFlags.Enqueue(isError);
        }

        private int CountErrorLines()
        {
            var count = 0;
            foreach (var isError in _errorFlags)
            {
                if (isError)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
#endif
