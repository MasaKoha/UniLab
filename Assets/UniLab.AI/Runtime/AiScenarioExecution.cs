#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>CLI とメールボックスでシナリオの起動・結果読取・完了待機を共有します。</summary>
    internal static class AiScenarioExecution
    {
        /// <summary>結果の予定パスを確定して既存ランナーを開始します。</summary>
        internal static AiCommandResponse Start(AiCommandArguments arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments.path))
            {
                throw new ArgumentException("path が必要です。");
            }

            AiCommandArguments.ValidateDuration(arguments.scenarioTimeoutSeconds, nameof(arguments.scenarioTimeoutSeconds), true);
            if (!Application.isPlaying)
            {
                return new AiCommandResponse { op = "scenario.run", message = "playMode が必要です" };
            }

            var projectDirectory = Path.GetDirectoryName(Application.dataPath);
            var scenarioPath = Path.GetFullPath(Path.Combine(projectDirectory, arguments.path));
            var scenarioJson = File.ReadAllText(scenarioPath);
            AiJsonObject.Parse(scenarioJson);
            var scenario = JsonUtility.FromJson<UiScenario>(scenarioJson);
            if (scenario == null)
            {
                throw new ArgumentException($"シナリオ JSON の読み込みに失敗しました。 path={scenarioPath}");
            }

            UiScenarioJsonPresence.Apply(scenarioJson, scenario);
            var scenarioName = string.IsNullOrEmpty(arguments.name) ? Path.GetFileNameWithoutExtension(scenarioPath) : arguments.name;
            // 同名シナリオの連続実行でも、前回の完了ファイルを今回の結果と誤認させない。
            var plannedPath = Path.GetFullPath(UiScenarioRunner.CreateResultFilePath(scenarioName));
            var resultDirectory = Path.GetDirectoryName(plannedPath) + "-" + Guid.NewGuid().ToString("N");
            var resultFilePath = Path.Combine(resultDirectory, Path.GetFileName(plannedPath));
            UiScenarioRunner.Run(scenario, scenarioName, resultFilePath);
            return new AiCommandResponse { ok = true, op = "scenario.run", path = resultFilePath, status = "running" };
        }

        /// <summary>書き込み途中の JSON は次のポーリングへ回します。</summary>
        internal static AiCommandResponse ReadStatus(string resultFilePath, string operation)
        {
            var response = new AiCommandResponse { ok = true, op = operation, path = resultFilePath ?? string.Empty, status = "running" };
            if (string.IsNullOrEmpty(resultFilePath) || !File.Exists(resultFilePath))
            {
                return response;
            }

            try
            {
                var resultJson = File.ReadAllText(resultFilePath);
                AiJsonObject.Parse(resultJson);
                var result = JsonUtility.FromJson<ScenarioResult>(resultJson);
                if (result == null || string.IsNullOrEmpty(result.verdict))
                {
                    return response;
                }

                response.status = "completed";
                response.verdict = result.verdict;
                response.failedSteps = result.failedSteps;
                response.warningCount = result.warningCount;
            }
            catch (Exception)
            {
                return response;
            }

            return response;
        }

        /// <summary>この要求で起動した結果だけを待ち、後続の CLI 実行と取り違えません。</summary>
        internal static IEnumerator<object> WaitAsync(AiCommandResponse response, float timeoutSeconds)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var status = ReadStatus(response.path, response.op);
                if (status.status == "completed")
                {
                    response.status = status.status;
                    response.verdict = status.verdict;
                    response.failedSteps = status.failedSteps;
                    response.warningCount = status.warningCount;
                    yield break;
                }

                if (stopwatch.Elapsed.TotalSeconds >= timeoutSeconds)
                {
                    response.ok = false;
                    response.error = "scenario timeout";
                    yield break;
                }

                yield return null;
            }
        }
    }
}
#endif
