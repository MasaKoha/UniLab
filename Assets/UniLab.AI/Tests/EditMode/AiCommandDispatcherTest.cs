#if UNITY_EDITOR
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>PlayMode 不要のゲートウェイ契約を検証します。</summary>
    public sealed class AiCommandDispatcherTest
    {
        /// <summary>未知の操作を成功扱いしないことを保証します。</summary>
        [Test]
        public void UnknownOperationReturnsFailure()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "missing" });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Is.EqualTo("unknown op"));
        }

        /// <summary>JsonUtility が寛容に読む入力も、プロトコル上は拒否します。</summary>
        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("{\"value\":1,}")]
        [TestCase("{} trailing")]
        [TestCase("{\"value\":01}")]
        [TestCase("{\"value\":\"\\q\"}")]
        public void InvalidArgumentsReturnFailure(string arguments)
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "ops", args = arguments });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Is.Not.Empty);
        }

        /// <summary>クライアントが操作一覧から必要な入口を発見できます。</summary>
        [Test]
        public void OperationsIncludeAgentAndCapture()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "ops" });
            Assert.That(response.ok, Is.True);
            Assert.That(response.text.Split('\n'), Is.EquivalentTo(AiCommandDispatcher.ListOps()));
            Assert.That(response.text, Does.Contain("agent.begin"));
            Assert.That(response.text, Does.Contain("agent.act"));
            Assert.That(response.text, Does.Contain("capture"));
        }

        /// <summary>ディレクトリ逸脱や空の撮影名を撮影前に拒否します。</summary>
        [TestCase("../x")]
        [TestCase("")]
        [TestCase("x/y")]
        [TestCase("x.png")]
        [TestCase("x\n")]
        public void InvalidCaptureNameReturnsFailure(string name)
        {
            var arguments = UnityEngine.JsonUtility.ToJson(new CaptureName { name = name });
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "capture", args = arguments });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Does.Contain("name"));
            Assert.That(response.path, Is.Empty);
        }

        /// <summary>Play 外の既存拒否文言を応答に保持します。</summary>
        [Test]
        public void AgentRequiresPlayMode()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "agent.observe" });
            Assert.That(response.ok, Is.False);
            Assert.That(response.message, Is.EqualTo("playMode が必要です"));
        }

        /// <summary>非同期入口の引数エラーも例外を漏らさず一度だけ通知します。</summary>
        [Test]
        public void AsyncInvalidArgumentsCompleteOnce()
        {
            var completionCount = 0;
            AiCommandResponse response = null;
            var execution = AiCommandDispatcher.ExecuteAsync(
                new AiCommandRequest { op = "ops", args = "{" },
                result =>
                {
                    completionCount++;
                    response = result;
                });
            Assert.That(execution.MoveNext(), Is.False);
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Is.Not.Empty);
        }

        /// <summary>Play 外の拒否だけで通過せず、scope 自体を検証します。</summary>
        [Test]
        public void InvalidObservationScopeReturnsFailure()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "agent.observe", args = "{\"scope\":\"foo\"}" });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Does.Contain("scope"));
        }

        [System.Serializable]
        private sealed class CaptureName
        {
            /// <summary>改行を含む名前も正しく JSON エスケープして検証します。</summary>
            public string name;
        }
    }
}
#endif
