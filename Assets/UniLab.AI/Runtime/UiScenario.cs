using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// UI 自動巡回シナリオ。JSON から JsonUtility で読み込む。
    /// </summary>
    [Serializable]
    public sealed class UiScenario
    {
        /// <summary>
        /// 撮影の出力先ディレクトリ。空なら DebugOutputPath の既定配下を使う。
        /// </summary>
        public string outputDirectory;

        /// <summary>
        /// 上から順に実行するシナリオステップ列。
        /// </summary>
        public UiScenarioStep[] steps;
    }
}
#endif
