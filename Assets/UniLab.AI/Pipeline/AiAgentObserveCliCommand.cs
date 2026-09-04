#if UNILAB_AI_PIPELINE
using Unity.Pipeline.Commands;
using UnityEngine;

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
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "agent.observe", args = JsonUtility.ToJson(new AiCliArguments { diffOnly = diffOnly }) });
            return JsonUtility.ToJson(response, true);
        }
    }
}
#endif
