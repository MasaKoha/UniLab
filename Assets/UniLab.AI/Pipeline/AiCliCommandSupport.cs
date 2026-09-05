#if UNILAB_AI_PIPELINE
using System;
using System.IO;
using UnityEditor;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// CLI ラッパ間で共有する最小限の状態と共通処理です。
    /// 既存 Runtime API へ依存を寄せ、各コマンド本体を薄く保つために用意します。
    /// </summary>
    internal static class AiCliCommandSupport
    {
        private const string PlayModeRequiredMessageText = "playMode が必要です";
        private const int ForensicsPreviewLineCount = 20;

        /// <summary>CLI 間で PlayMode 要求の文言を揃えます。</summary>
        internal static string PlayModeRequiredMessage
        {
            get
            {
                return PlayModeRequiredMessageText;
            }
        }

        /// <summary>Runtime の操作を開始できる Editor 状態かを確認します。</summary>
        internal static bool IsPlayModeActive()
        {
            return EditorApplication.isPlaying;
        }

        /// <summary>時刻順の成果物から最新の保存先を選びます。</summary>
        internal static string GetLatestDirectoryPath(string rootDirectoryPath)
        {
            if (string.IsNullOrEmpty(rootDirectoryPath) || !Directory.Exists(rootDirectoryPath))
            {
                return string.Empty;
            }

            var directoryPaths = Directory.GetDirectories(rootDirectoryPath);
            if (directoryPaths.Length == 0)
            {
                return string.Empty;
            }

            Array.Sort(directoryPaths, StringComparer.Ordinal);
            return directoryPaths[directoryPaths.Length - 1];
        }

        /// <summary>大量のログを CLI 応答へ流さないよう先頭だけを返します。</summary>
        internal static string[] ReadFirstLines(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return Array.Empty<string>();
            }

            var lines = File.ReadAllLines(filePath);
            if (lines.Length <= ForensicsPreviewLineCount)
            {
                return lines;
            }

            var previewLines = new string[ForensicsPreviewLineCount];
            Array.Copy(lines, previewLines, ForensicsPreviewLineCount);
            return previewLines;
        }
    }
}
#endif
