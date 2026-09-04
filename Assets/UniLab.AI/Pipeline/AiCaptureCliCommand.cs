#if UNILAB_AI_PIPELINE
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniLab.AI.Pipeline
{
    /// <summary>画面撮影の即時要求を CLI へ公開します。</summary>
    public static class AiCaptureCliCommand
    {
        /// <summary>撮影を要求し、保存予定の絶対パスを返します。</summary>
        [CliCommand("ai_capture", "画面撮影を要求します。", Tags = new[] { "ai" })]
        public static string Capture(
            [CliArg("name", "英数字・_・- の撮影名。", Required = true)] string name,
            [CliArg("directory", "保存先ディレクトリ。")] string directory = "")
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest
            {
                op = "capture",
                args = JsonUtility.ToJson(new AiCliArguments { name = name, directory = directory }),
            });
            return JsonUtility.ToJson(response, true);
        }
    }
}
#endif
