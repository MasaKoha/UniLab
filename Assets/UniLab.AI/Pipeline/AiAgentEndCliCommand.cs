#if UNILAB_AI_PIPELINE
using UniLab.AI;
using Unity.Pipeline.Commands;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// エージェントセッション終了を Unity 公式 CLI へ公開します。
    /// 破棄処理は Runtime 側へ委譲し、この層では入口だけ提供します。
    /// </summary>
    public static class AiAgentEndCliCommand
    {
        /// <summary>
        /// 現在セッションを終了します。
        /// </summary>
        [CliCommand("ai_agent_end", "エージェントセッションを終了します。", Tags = new[] { "agent" })]
        public static string End()
        {
            if (!AiCliCommandSupport.IsPlayModeActive())
            {
                return AiCliCommandSupport.PlayModeRequiredMessage;
            }

            return AgentSessionCommands.End();
        }
    }
}
#endif
