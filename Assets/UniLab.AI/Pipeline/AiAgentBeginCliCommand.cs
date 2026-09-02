#if UNILAB_AI_PIPELINE
using UniLab.AI;
using Unity.Pipeline.Commands;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// エージェントセッション開始を Unity 公式 CLI へ公開します。
    /// 既存の `AgentSessionCommands.Begin` をそのまま呼びます。
    /// </summary>
    public static class AiAgentBeginCliCommand
    {
        /// <summary>
        /// エージェントセッションを開始します。
        /// </summary>
        [CliCommand("ai_agent_begin", "エージェントセッションを開始します。", Tags = new[] { "agent" })]
        public static string Begin(
            [CliArg("goal", "目標 JSON 文字列。", Required = true)] string goal,
            [CliArg("options", "オプション JSON 文字列。")] string options = "")
        {
            if (!AiCliCommandSupport.IsPlayModeActive())
            {
                return AiCliCommandSupport.PlayModeRequiredMessage;
            }

            return AgentSessionCommands.Begin(goal, options);
        }
    }
}
#endif
