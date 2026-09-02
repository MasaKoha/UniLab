#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// 数値化した結果をファイルへ固定し、後続ツールが実行時状態に依存せず再利用できるようにする。
    /// </summary>
    [Serializable]
    public sealed class PerformanceReport
    {
        /// <summary>
        /// ファイル名へシナリオ名を残し、後からランの意図を辿れるようにする。
        /// </summary>
        public string scenario;

        /// <summary>
        /// 同じシナリオを複数回回したときの比較基準にするため、開始時刻を保持する。
        /// </summary>
        public string startedAt;

        /// <summary>
        /// 操作単位のボトルネックを切り分けるため、集計はステップ配列で保持する。
        /// </summary>
        public PerformanceStepReport[] steps;

        /// <summary>
        /// 全体傾向を一目で読めるよう、ステップ詳細とは別に総括値を持つ。
        /// </summary>
        public PerformanceSummaryReport summary;

        /// <summary>
        /// 後続処理が `JsonUtility` でそのまま読める形に統一する。
        /// </summary>
        public PerformanceReport(string scenarioName, string startedAtText, PerformanceStepReport[] stepReports, PerformanceSummaryReport summaryReport)
        {
            scenario = string.IsNullOrEmpty(scenarioName) ? string.Empty : scenarioName;
            startedAt = string.IsNullOrEmpty(startedAtText) ? string.Empty : startedAtText;
            steps = stepReports ?? Array.Empty<PerformanceStepReport>();
            summary = summaryReport;
        }

        /// <summary>
        /// 既定出力先へ保存し、AI や後続メニューがファイルパスだけで結果へ到達できるようにする。
        /// </summary>
        public string Save()
        {
            var performanceDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, "performance");
            Directory.CreateDirectory(performanceDirectoryPath);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var safeScenarioName = SanitizeFileName(scenario);
            var filePath = Path.Combine(performanceDirectoryPath, $"{safeScenarioName}-{timestamp}.json");
            File.WriteAllText(filePath, JsonUtility.ToJson(this, true));
            return filePath;
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "scenario";
            }

            var sanitizedName = fileName;
            var invalidCharacters = Path.GetInvalidFileNameChars();
            for (var characterIndex = 0; characterIndex < invalidCharacters.Length; characterIndex++)
            {
                sanitizedName = sanitizedName.Replace(invalidCharacters[characterIndex], '_');
            }

            return string.IsNullOrEmpty(sanitizedName) ? "scenario" : sanitizedName;
        }
    }
}
#endif
