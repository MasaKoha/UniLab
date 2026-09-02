#if UNILAB_AI_PIPELINE
using UniLab.AI;
using Unity.Pipeline.Commands;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// エージェント観測を Unity 公式 CLI へ公開します。
    /// 観測の整形は Runtime 側へ集約し、この層では中継だけを行います。
    /// </summary>
    public static class AiAgentObserveCliCommand
    {
        /// <summary>
        /// 現在セッションの観測を返します。
        /// </summary>
        [CliCommand("ai_agent_observe", "エージェントの現在観測を返します。", Tags = new[] { "agent" })]
        public static string Observe(
            [CliArg("diffOnly", "前回との差分だけ返すか。")] bool diffOnly = false)
        {
            if (!AiCliCommandSupport.IsPlayModeActive())
            {
                return AiCliCommandSupport.PlayModeRequiredMessage;
            }

            return AgentSessionCommands.Observe(diffOnly);
        }
    }
}
#endif
