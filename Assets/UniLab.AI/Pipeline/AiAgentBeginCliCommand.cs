#if UNILAB_AI_PIPELINE
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// エージェントセッション開始を Unity 公式 CLI へ公開します。
    /// 引数検証と実行を AI ゲートウェイへ委譲します。
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
            var optionsJson = string.IsNullOrWhiteSpace(options) ? "{}" : options;
            var response = AiCommandDispatcher.Execute(new AiCommandRequest
            {
                op = "agent.begin",
                args = "{\"goal\":" + goal + ",\"options\":" + optionsJson + "}",
            });
            return JsonUtility.ToJson(response, true);
        }
    }
}
#endif
