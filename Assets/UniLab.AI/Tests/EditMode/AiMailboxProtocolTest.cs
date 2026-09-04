#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UniLab.AI.Tests
{
    /// <summary>書きかけの隔離と既存クライアント向けの JSON キーを検証します。</summary>
    public sealed class AiMailboxProtocolTest
    {
        private string _directory;

        /// <summary>並列実行でも衝突しない一時領域を使います。</summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "unilab-mailbox-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        /// <summary>テストの生成物を残しません。</summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_directory, true);
        }

        /// <summary>内側の JSON 文字列と日本語の本文を壊さず往復します。</summary>
        [Test]
        public void RequestAndResponseRoundTrip()
        {
            var request = new AiCommandRequest { op = "agent.act", args = "{\"action\":{\"submit\":\"開始\"}}" };
            var restoredRequest = JsonUtility.FromJson<AiCommandRequest>(JsonUtility.ToJson(request));
            Assert.That(restoredRequest.op, Is.EqualTo(request.op));
            Assert.That(restoredRequest.args, Is.EqualTo(request.args));
            var response = new AiCommandResponse
            {
                ok = true, op = request.op, session = "session", message = "完了", text = "本文\n次行",
                path = "/captures/test.png", settled = true, error = "",
            };
            var json = JsonUtility.ToJson(response);
            var restoredResponse = JsonUtility.FromJson<AiCommandResponse>(json);
            Assert.That(JsonUtility.ToJson(restoredResponse), Is.EqualTo(json));
            var legacy = JsonUtility.FromJson<AgentCommandResult>(json);
            Assert.That(legacy.ok, Is.True);
            Assert.That(legacy.session, Is.EqualTo(response.session));
            Assert.That(legacy.message, Is.EqualTo(response.message));
            Assert.That(legacy.text, Is.EqualTo(response.text));
            Assert.That(legacy.path, Is.EqualTo(response.path));
        }

        /// <summary>書きかけは列挙も直接読み込みもできず、rename 後だけ読めます。</summary>
        [Test]
        public void TemporaryRequestIsNeverRead()
        {
            var path = Path.Combine(_directory, "req-test.json");
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, "{\"op\":");
            Assert.That(AiMailboxFiles.GetRequests(_directory), Is.Empty);
            Assert.Throws<ArgumentException>(() => AiMailboxFiles.ReadRequest(temporaryPath));
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(new AiCommandRequest { op = "ping" }));
            File.Move(temporaryPath, path);
            Assert.That(AiMailboxFiles.GetRequests(_directory), Is.EqualTo(new[] { path }));
            Assert.That(AiMailboxFiles.ReadRequest(path).op, Is.EqualTo("ping"));
        }

        /// <summary>公開済みの応答は再書き込みで破損しません。</summary>
        [Test]
        public void AtomicWritePreservesPublishedFile()
        {
            var path = Path.Combine(_directory, "res-test.json");
            AiMailboxFiles.WriteAtomic(path, "{\"ok\":true}");
            Assert.Throws<IOException>(() => AiMailboxFiles.WriteAtomic(path, "{\"ok\":false}"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{\"ok\":true}"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        /// <summary>応答の公開後に要求を消し、完了済みの要求を再実行しません。</summary>
        [Test]
        public void CompletionPublishesResponseBeforeRemovingRequest()
        {
            var path = Path.Combine(_directory, "req-test.json");
            AiMailboxFiles.WriteAtomic(path, JsonUtility.ToJson(new AiCommandRequest { op = "ping" }));
            AiMailboxFiles.Complete(path, new AiCommandResponse { ok = true, op = "ping" });
            Assert.That(File.Exists(path), Is.False);
            var response = JsonUtility.FromJson<AiCommandResponse>(File.ReadAllText(AiMailboxFiles.GetResponsePath(path)));
            Assert.That(response.ok, Is.True);
        }

        /// <summary>正式ファイルでも壊れた JSON は実行しません。</summary>
        [Test]
        public void MalformedPublishedRequestIsRejected()
        {
            var path = Path.Combine(_directory, "req-test.json");
            File.WriteAllText(path, "{");
            Assert.Throws<FormatException>(() => AiMailboxFiles.ReadRequest(path));
        }
    }
}
#endif
