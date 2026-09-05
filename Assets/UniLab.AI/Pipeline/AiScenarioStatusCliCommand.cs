#if UNILAB_AI_PIPELINE
using UniLab.AI;
using Unity.Pipeline.Commands;

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
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "scenario.status" });
            return response.status == "completed"
                ? AiScenarioStatusResult.CreateCompleted(response.path, response.verdict, response.failedSteps, response.warningCount)
                : AiScenarioStatusResult.CreateRunning(response.path);
        }
    }
}
#endif
