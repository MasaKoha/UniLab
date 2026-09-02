using UnityEditor;
using UnityEngine;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// RunArchive の生成と索引再構築をメニュー化し、外部自動化なしでも同じ運用を再現できるようにする。
    /// </summary>
    public static class RunArchiveMenu
    {
        private const string ArchiveLatestMenuPath = "UniLab/Debug/Run Archive/Archive Latest";
        private const string ArchiveScenarioResultMenuPath = "UniLab/Debug/Run Archive/Archive Scenario Result...";
        private const string RebuildIndexMenuPath = "UniLab/Debug/Run Archive/Rebuild Index";

        /// <summary>
        /// 直近の成果物束をすぐ集約し、検証直後の目視フローを最短化する。
        /// </summary>
        [MenuItem(ArchiveLatestMenuPath)]
        private static void ArchiveLatest()
        {
            var archiveDirectoryPath = RunArchive.CreateLatest();
            if (string.IsNullOrEmpty(archiveDirectoryPath))
            {
                return;
            }

            UnityEngine.Debug.Log($"[RunArchive] ランを集約しました。 path={archiveDirectoryPath}");
            EditorUtility.RevealInFinder(archiveDirectoryPath);
        }

        /// <summary>
        /// 特定の結果 JSON を起点に集約し直せるようにし、過去ランの再編成や失敗解析をやり直しやすくする。
        /// </summary>
        [MenuItem(ArchiveScenarioResultMenuPath)]
        private static void ArchiveScenarioResult()
        {
            var scenarioResultPath = EditorUtility.OpenFilePanel("シナリオ結果 JSON を選択", DebugOutputPath.DirectoryPath, "json");
            if (string.IsNullOrEmpty(scenarioResultPath))
            {
                return;
            }

            var archiveDirectoryPath = RunArchive.CreateFromScenarioResult(scenarioResultPath);
            if (string.IsNullOrEmpty(archiveDirectoryPath))
            {
                return;
            }

            UnityEngine.Debug.Log($"[RunArchive] ランを集約しました。 path={archiveDirectoryPath}");
            EditorUtility.RevealInFinder(archiveDirectoryPath);
        }

        /// <summary>
        /// 配下ランを動かした後でも索引だけを再生成できるようにし、ギャラリーが古い一覧を持たないようにする。
        /// </summary>
        [MenuItem(RebuildIndexMenuPath)]
        private static void RebuildIndex()
        {
            var indexPath = RunArchive.RebuildIndex();
            if (string.IsNullOrEmpty(indexPath))
            {
                return;
            }

            UnityEngine.Debug.Log($"[RunArchive] index を再生成しました。 path={indexPath}");
            EditorUtility.RevealInFinder(indexPath);
        }
    }
}
