#if UNILAB_AI_PIPELINE
using UniLab.AI;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// シナリオ JSON の起動を Unity 公式 CLI へ公開します。
    /// 結果受け取りは既存ランナーと同じ結果ファイルポーリングへ寄せます。
    /// </summary>
    public static class AiScenarioRunCliCommand
    {
        /// <summary>
        /// シナリオを開始し、結果 JSON の予定パスを即時に返します。
        /// </summary>
        [CliCommand("ai_scenario_run", "UI シナリオを開始し、結果 JSON のパスを返します。", Tags = new[] { "ui" })]
        public static string Run(
            [CliArg("path", "シナリオ JSON ファイルのパス。", Required = true)] string path,
            [CliArg("name", "結果表示に使うシナリオ名。")] string name = "")
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest
            {
                op = "scenario.run",
                args = JsonUtility.ToJson(new ScenarioArguments { path = path, name = name }),
            });
            return response.ok ? response.path : string.IsNullOrEmpty(response.error) ? response.message : response.error;
        }

        [System.Serializable]
        private sealed class ScenarioArguments
        {
            /// <summary>CLI の指定を共通の引数へ渡します。</summary>
            public string path;
            /// <summary>結果表示名をゲートウェイへ渡します。</summary>
            public string name;
        }
    }
}
#endif
