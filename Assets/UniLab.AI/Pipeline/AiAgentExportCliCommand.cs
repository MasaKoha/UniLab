#if UNILAB_AI_PIPELINE
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// エージェントセッションのシナリオ書き出しを Unity 公式 CLI へ公開します。
    /// 成功セッションの保存形式は Runtime 側の既定へ揃えます。
    /// </summary>
    public static class AiAgentExportCliCommand
    {
        /// <summary>
        /// 現在セッションをシナリオとして書き出します。
        /// </summary>
        [CliCommand("ai_agent_export", "現在のエージェントセッションをシナリオ保存します。", Tags = new[] { "agent" })]
        public static string Export(
            [CliArg("name", "出力シナリオ名。")] string name = "")
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "agent.export", args = JsonUtility.ToJson(new AiCliArguments { name = name }) });
            return JsonUtility.ToJson(response, true);
        }
    }
}
#endif
