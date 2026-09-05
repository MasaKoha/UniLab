#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UniLab.AI.Tests
{
    /// <summary>合成した結果ファイルで完了・未完了・タイムアウトの契約を検証します。</summary>
    public sealed class AiScenarioExecutionTest
    {
        private const float TimeoutSeconds = 1f;
        private const int FailedStepCount = 2;
        private const int WarningCount = 3;
        private string _resultPath;

        /// <summary>実シナリオの結果と独立したファイルを使います。</summary>
        [SetUp]
        public void SetUp()
        {
            _resultPath = Path.Combine(Path.GetTempPath(), "unilab-scenario-" + Guid.NewGuid().ToString("N") + ".json");
        }

        /// <summary>テスト用の結果ファイルだけを削除します。</summary>
        [TearDown]
        public void TearDown()
        {
            File.Delete(_resultPath);
        }

        /// <summary>回帰で fail になっても通信は成功として合否と集計を返します。</summary>
        [Test]
        public void CompletedScenarioReturnsVerdictAndResultPath()
        {
            File.WriteAllText(_resultPath, JsonUtility.ToJson(new ScenarioResult { verdict = "fail", failedSteps = FailedStepCount, warningCount = WarningCount }));
            var response = new AiCommandResponse { ok = true, op = "scenario.run", path = _resultPath, status = "running" };
            using (var execution = AiScenarioExecution.WaitAsync(response, TimeoutSeconds))
            {
                Assert.That(execution.MoveNext(), Is.False);
            }

            Assert.That(response.ok, Is.True);
            Assert.That(response.status, Is.EqualTo("completed"));
            Assert.That(response.path, Is.EqualTo(_resultPath));
            Assert.That(response.verdict, Is.EqualTo("fail"));
            Assert.That(response.failedSteps, Is.EqualTo(FailedStepCount));
            Assert.That(response.warningCount, Is.EqualTo(WarningCount));
        }

        /// <summary>未完成の JSON に verdict が見えても完了と誤認しません。</summary>
        [Test]
        public void PartiallyWrittenResultRemainsRunning()
        {
            File.WriteAllText(_resultPath, "{\"verdict\":\"pass\"");
            var response = AiScenarioExecution.ReadStatus(_resultPath, "scenario.status");
            Assert.That(response.status, Is.EqualTo("running"));
            Assert.That(response.verdict, Is.Empty);
        }

        /// <summary>待機期限を超えても後から status で回収できる予定パスを残します。</summary>
        [Test]
        public void TimeoutPreservesResultPathWithoutVerdict()
        {
            var response = new AiCommandResponse { ok = true, op = "scenario.run", path = _resultPath, status = "running" };
            using (var execution = AiScenarioExecution.WaitAsync(response, 0f))
            {
                Assert.That(execution.MoveNext(), Is.False);
            }

            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Is.EqualTo("scenario timeout"));
            Assert.That(response.path, Is.EqualTo(_resultPath));
            Assert.That(response.verdict, Is.Empty);
        }
    }
}
#endif
