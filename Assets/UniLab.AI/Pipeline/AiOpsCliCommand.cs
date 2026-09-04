#if UNILAB_AI_PIPELINE
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniLab.AI.Pipeline
{
    /// <summary>利用可能なゲートウェイ操作を CLI へ公開します。</summary>
    public static class AiOpsCliCommand
    {
        /// <summary>登録済み操作を改行区切りで返します。</summary>
        [CliCommand("ai_ops", "AI 操作一覧を返します。", Tags = new[] { "ai" })]
        public static string List()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "ops" });
            return JsonUtility.ToJson(response, true);
        }
    }
}
#endif
