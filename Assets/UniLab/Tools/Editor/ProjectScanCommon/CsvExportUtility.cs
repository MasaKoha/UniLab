using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.ProjectScanCommon
{
    /// <summary>
    /// Provides shared CSV export helpers used across project scan editor tools.
    /// </summary>
    public static class CsvExportUtility
    {
        /// <summary>
        /// Shows a SaveFilePanel and returns the selected path, or null if cancelled.
        /// </summary>
        public static string ShowSavePanel(string title, string defaultFileName)
        {
            var path = EditorUtility.SaveFilePanel(title, "", defaultFileName, "csv");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return path;
        }

        /// <summary>
        /// Writes the CSV content to the specified file path and logs the result.
        /// </summary>
        public static void WriteAndLog(string filePath, StringBuilder builder)
        {
            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
            Debug.Log($"CSV exported to: {filePath}");
        }
    }
}
