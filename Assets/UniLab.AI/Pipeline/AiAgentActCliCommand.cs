#if UNILAB_AI_PIPELINE
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// エージェント行動実行を Unity 公式 CLI へ公開します。
    /// 行動 JSON の解釈は Runtime 側の既存実装へ委譲します。
    /// </summary>
    public static class AiAgentActCliCommand
    {
        /// <summary>
        /// 現在セッションに 1 手を送ります。
        /// </summary>
        [CliCommand("ai_agent_act", "エージェントの 1 手を実行します。", Tags = new[] { "agent" })]
        public static string Act(
            [CliArg("action", "行動 JSON 文字列。", Required = true)] string action)
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "agent.act", args = "{\"action\":" + action + "}" });
            return JsonUtility.ToJson(response, true);
        }
    }
}
#endif
