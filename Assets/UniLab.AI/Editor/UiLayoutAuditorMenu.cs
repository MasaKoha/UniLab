using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// UI レイアウト監査を JSON へ保存するエディタメニューです。
    /// </summary>
    public static class UiLayoutAuditorMenu
    {
        private const string MenuPath = "UniLab/Debug/Audit UI Layout";
        private const string FileNamePrefix = "ui-audit-";
        private const string FileNameTimestampFormat = "yyyyMMdd-HHmmss";
        private const string FileExtension = ".json";

        /// <summary>
        /// ロード済み Canvas の UI レイアウト監査を実行し JSON 保存します。
        /// </summary>
        [MenuItem(MenuPath)]
        private static void AuditUiLayout()
        {
            var report = UiLayoutAuditor.Audit();
            Directory.CreateDirectory(DebugOutputPath.DirectoryPath);

            var timestamp = DateTime.Now.ToString(FileNameTimestampFormat);
            var outputFilePath = Path.Combine(DebugOutputPath.DirectoryPath, $"{FileNamePrefix}{timestamp}{FileExtension}");
            var json = JsonUtility.ToJson(report, true);
            File.WriteAllText(outputFilePath, json);

            var entryCount = report.entries == null ? 0 : report.entries.Length;
            UnityEngine.Debug.Log($"UI レイアウト監査が完了しました。 entries={entryCount}, path={outputFilePath}");
        }
    }
}
