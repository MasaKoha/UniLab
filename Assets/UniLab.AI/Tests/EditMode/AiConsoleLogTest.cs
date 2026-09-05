#if UNITY_EDITOR
using NUnit.Framework;
using System;
using UnityEngine;
namespace UniLab.AI.Tests
{
    /// <summary>PR4 の観測契約をシーンなしで検証します。</summary>
    public sealed class AiConsoleLogTest
    {
        private const int Capacity = 500;
        private const int OverflowCount = 5;

        /// <summary>容量を超えた古い行だけが破棄されます。</summary>
        [Test]
        public void OverflowDiscardsOldestLines()
        {
            using (var console = new AiConsoleLog())
            {
                for (var lineIndex = 0; lineIndex < Capacity + OverflowCount; lineIndex++)
                {
                    console.OnLogMessage($"line-{lineIndex}", string.Empty, LogType.Log);
                }

                var lines = console.Read(Capacity + OverflowCount).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                Assert.That(lines.Length, Is.EqualTo(Capacity));
                Assert.That(lines[0], Is.EqualTo($"[Log] line-{OverflowCount}"));
                Assert.That(lines[Capacity - 1], Is.EqualTo($"[Log] line-{Capacity + OverflowCount - 1}"));
            }
        }

        /// <summary>エラー抽出は Log と Warning を除き、本文とスタックを残します。</summary>
        [TestCase(LogType.Error)]
        [TestCase(LogType.Exception)]
        [TestCase(LogType.Assert)]
        public void ErrorLevelIncludesOnlyErrorAndStack(LogType type)
        {
            using (var console = new AiConsoleLog())
            {
                console.OnLogMessage("normal", "ignored-normal-stack", LogType.Log);
                console.OnLogMessage("problem", "stack-one", type);
                console.OnLogMessage("warning", "ignored-warning-stack", LogType.Warning);
                Assert.That(console.Read(Capacity, "error"), Is.EqualTo($"[{type}] problem{Environment.NewLine}stack-one"));
                Assert.That(console.Read(Capacity), Does.Not.Contain("ignored"));
            }
        }

        /// <summary>スタックは CRLF を含めて先頭 3 行までに制限します。</summary>
        [Test]
        public void StackTraceIncludesOnlyFirstThreeLines()
        {
            using (var console = new AiConsoleLog())
            {
                console.OnLogMessage("problem", "first\r\nsecond\r\nthird\r\nfourth", LogType.Exception);
                var expected = string.Join(Environment.NewLine, new[] { "[Exception] problem", "first", "second", "third" });
                Assert.That(console.Read(Capacity), Is.EqualTo(expected));
            }
        }

        /// <summary>本文が容量から落ちてもスタックのレベル情報を失いません。</summary>
        [Test]
        public void ErrorStackRetainsLevelAfterHeaderEviction()
        {
            using (var console = new AiConsoleLog())
            {
                console.OnLogMessage("problem", "remaining-stack", LogType.Exception);
                for (var lineIndex = 0; lineIndex < Capacity - 1; lineIndex++)
                {
                    console.OnLogMessage("normal", string.Empty, LogType.Log);
                }

                Assert.That(console.Read(Capacity, "error"), Is.EqualTo("remaining-stack"));
                Assert.That(console.Read(0), Is.Empty);
            }
        }
    }
}
#endif
