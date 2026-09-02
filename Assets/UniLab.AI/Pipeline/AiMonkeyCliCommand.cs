#if UNILAB_AI_PIPELINE
using System.IO;
using UniLab.AI;
using Unity.Pipeline.Commands;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// モンキーテスター起動を Unity 公式 CLI へ公開します。
    /// 実行ロジックは Runtime 側に残し、開始と出力先通知だけを担当します。
    /// </summary>
    public static class AiMonkeyCliCommand
    {
        private const string MonkeyDirectoryName = "monkey";

        /// <summary>
        /// モンキーテスターを開始し、出力ディレクトリを返します。
        /// </summary>
        [CliCommand("ai_monkey", "モンキーテスターを開始し、出力先を返します。", Tags = new[] { "monkey" })]
        public static string Start(
            [CliArg("seed", "乱数シード。")] int seed = 0,
            [CliArg("maxSteps", "最大手数。")] int maxSteps = 0,
            [CliArg("maxSeconds", "最大秒数。")] float maxSeconds = 0.0f)
        {
            if (!AiCliCommandSupport.IsPlayModeActive())
            {
                return AiCliCommandSupport.PlayModeRequiredMessage;
            }

            var options = new MonkeyOptions
            {
                seed = seed,
                maxSteps = maxSteps,
                maxSeconds = maxSeconds,
            };
            MonkeyTester.Start(options);
            var monkeyRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, MonkeyDirectoryName);
            return AiCliCommandSupport.GetLatestDirectoryPath(monkeyRootDirectoryPath);
        }
    }
}
#endif
