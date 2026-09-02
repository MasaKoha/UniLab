#if UNILAB_AI_PIPELINE
using System;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// `ai_scenario_status` の返却形式です。
    /// 完了前と完了後でポーリング側の分岐を固定化するために使います。
    /// </summary>
    [Serializable]
    internal sealed class AiScenarioStatusResult
    {
        public string status;
        public string resultFilePath;
        public string verdict;
        public int failedSteps;
        public int warningCount;

        internal static AiScenarioStatusResult CreateRunning(string resultFilePath)
        {
            return new AiScenarioStatusResult
            {
                status = "running",
                resultFilePath = resultFilePath ?? string.Empty,
                verdict = string.Empty,
                failedSteps = 0,
                warningCount = 0,
            };
        }

        internal static AiScenarioStatusResult CreateCompleted(string resultFilePath, string verdict, int failedSteps, int warningCount)
        {
            return new AiScenarioStatusResult
            {
                status = "completed",
                resultFilePath = resultFilePath ?? string.Empty,
                verdict = verdict ?? string.Empty,
                failedSteps = failedSteps,
                warningCount = warningCount,
            };
        }
    }
}
#endif
