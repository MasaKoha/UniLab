#if UNILAB_AI_PIPELINE
using System;
using System.IO;
using UniLab.AI;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// 直前のシナリオ実行状態を Unity 公式 CLI へ公開します。
    /// ファイル出力済みかどうかだけを見て、待機は呼び出し側へ委ねます。
    /// </summary>
    public static class AiScenarioStatusCliCommand
    {
        /// <summary>
        /// 直前のシナリオ結果ファイルを確認し、未完了なら running を返します。
        /// </summary>
        [CliCommand("ai_scenario_status", "直前の UI シナリオ実行状態を返します。", Tags = new[] { "ui" })]
        public static object GetStatus()
        {
            var resultFilePath = AiCliCommandSupport.LastScenarioResultFilePath;
            if (string.IsNullOrEmpty(resultFilePath) || !File.Exists(resultFilePath))
            {
                return AiScenarioStatusResult.CreateRunning(resultFilePath);
            }

            try
            {
                var resultJson = File.ReadAllText(resultFilePath);
                var result = JsonUtility.FromJson<ScenarioResult>(resultJson);
                if (result == null || string.IsNullOrEmpty(result.verdict))
                {
                    return AiScenarioStatusResult.CreateRunning(resultFilePath);
                }

                return AiScenarioStatusResult.CreateCompleted(
                    resultFilePath,
                    result.verdict,
                    result.failedSteps,
                    result.warningCount);
            }
            catch (Exception)
            {
                return AiScenarioStatusResult.CreateRunning(resultFilePath);
            }
        }
    }
}
#endif
