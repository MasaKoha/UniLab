#if UNILAB_AI_PIPELINE
using System.IO;
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
            if (!AiCliCommandSupport.IsPlayModeActive())
            {
                return AiCliCommandSupport.PlayModeRequiredMessage;
            }

            var scenarioPath = Path.GetFullPath(path);
            if (!File.Exists(scenarioPath))
            {
                return $"シナリオファイルが見つかりません。 path={scenarioPath}";
            }

            var scenarioJson = File.ReadAllText(scenarioPath);
            var scenario = JsonUtility.FromJson<UiScenario>(scenarioJson);
            if (scenario == null)
            {
                return $"シナリオ JSON の読み込みに失敗しました。 path={scenarioPath}";
            }

            UiScenarioJsonPresence.Apply(scenarioJson, scenario);
            var scenarioName = AiCliCommandSupport.ResolveScenarioName(name, scenarioPath);
            var resultFilePath = UiScenarioRunner.CreateResultFilePath(scenarioName);
            UiScenarioRunner.Run(scenario, scenarioName, resultFilePath);
            AiCliCommandSupport.LastScenarioResultFilePath = resultFilePath;
            return resultFilePath;
        }
    }
}
#endif
