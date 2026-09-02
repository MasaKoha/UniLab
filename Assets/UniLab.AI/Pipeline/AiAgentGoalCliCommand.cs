#if UNILAB_AI_PIPELINE
using UniLab.AI;
using Unity.Pipeline.Commands;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// エージェント目標達成判定を Unity 公式 CLI へ公開します。
    /// 判定ロジックは Runtime 側の `AgentSession` に閉じ込めます。
    /// </summary>
    public static class AiAgentGoalCliCommand
    {
        /// <summary>
        /// 現在セッションの目標達成状態を返します。
        /// </summary>
        [CliCommand("ai_agent_goal", "エージェントの目標達成状態を返します。", Tags = new[] { "agent" })]
        public static string Goal()
        {
            if (!AiCliCommandSupport.IsPlayModeActive())
            {
                return AiCliCommandSupport.PlayModeRequiredMessage;
            }

            return AgentSessionCommands.IsGoalReached();
        }
    }
}
#endif
