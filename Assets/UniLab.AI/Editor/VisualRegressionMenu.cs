using System.IO;
using UnityEditor;
using UnityEngine;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// ベースライン比較と受け入れ更新をメニュー化し、外部自動化なしでも同じ運用を再現できるようにする。
    /// </summary>
    [InitializeOnLoad]
    public static class VisualRegressionMenu
    {
        private const string CompareMenuPath = "UniLab/Debug/Visual Regression/Compare...";
        private const string AcceptAllMenuPath = "UniLab/Debug/Visual Regression/Accept All";
        private const string AcceptOneMenuPath = "UniLab/Debug/Visual Regression/Accept One...";
        private const string LastCapturesDirectoryKey = "UniLab.AI.VisualRegression.LastCapturesDirectory";
        private const string LastBaselinesDirectoryKey = "UniLab.AI.VisualRegression.LastBaselinesDirectory";
        private const string DefaultBaselinesDirectoryName = "Baselines";

        static VisualRegressionMenu()
        {
        }

        /// <summary>
        /// 比較対象ディレクトリを都度選ばせ、利用側リポジトリごとの差異を UniLab.AI 本体へ持ち込まない。
        /// </summary>
        [MenuItem(CompareMenuPath)]
        private static void CompareCaptures()
        {
            var capturesDirectory = SelectCapturesDirectory();
            if (string.IsNullOrEmpty(capturesDirectory))
            {
                return;
            }

            var baselinesDirectory = SelectBaselinesDirectory();
            if (string.IsNullOrEmpty(baselinesDirectory))
            {
                return;
            }

            var reportPath = VisualRegression.Compare(capturesDirectory, baselinesDirectory, new VisualRegressionOptions());
            SessionState.SetString(LastCapturesDirectoryKey, capturesDirectory);
            SessionState.SetString(LastBaselinesDirectoryKey, baselinesDirectory);
            UnityEngine.Debug.Log($"[VisualRegression] 比較が完了しました。 report={reportPath}");
            EditorUtility.RevealInFinder(reportPath);
        }

        /// <summary>
        /// 直近の比較元を覚えておき、意図した UI 変更後のベースライン更新を 1 操作で終えられるようにする。
        /// </summary>
        [MenuItem(AcceptAllMenuPath)]
        private static void AcceptAll()
        {
            if (!TryResolveStoredDirectories(out var capturesDirectory, out var baselinesDirectory))
            {
                capturesDirectory = SelectCapturesDirectory();
                baselinesDirectory = SelectBaselinesDirectory();
            }

            if (string.IsNullOrEmpty(capturesDirectory) || string.IsNullOrEmpty(baselinesDirectory))
            {
                return;
            }

            VisualRegression.AcceptAll(capturesDirectory, baselinesDirectory);
            SessionState.SetString(LastCapturesDirectoryKey, capturesDirectory);
            SessionState.SetString(LastBaselinesDirectoryKey, baselinesDirectory);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[VisualRegression] ベースラインを一括更新しました。 captures={capturesDirectory} baselines={baselinesDirectory}");
        }

        /// <summary>
        /// 1 枚だけ受け入れる導線を分け、失敗 capture の局所更新を安全に行えるようにする。
        /// </summary>
        [MenuItem(AcceptOneMenuPath)]
        private static void AcceptOne()
        {
            if (!TryResolveStoredDirectories(out var capturesDirectory, out var baselinesDirectory))
            {
                capturesDirectory = SelectCapturesDirectory();
                baselinesDirectory = SelectBaselinesDirectory();
            }

            if (string.IsNullOrEmpty(capturesDirectory) || string.IsNullOrEmpty(baselinesDirectory))
            {
                return;
            }

            var captureFilePath = EditorUtility.OpenFilePanel("受け入れる capture を選択", capturesDirectory, "png");
            if (string.IsNullOrEmpty(captureFilePath))
            {
                return;
            }

            var captureName = Path.GetFileNameWithoutExtension(captureFilePath);
            VisualRegression.Accept(captureName, capturesDirectory, baselinesDirectory);
            SessionState.SetString(LastCapturesDirectoryKey, capturesDirectory);
            SessionState.SetString(LastBaselinesDirectoryKey, baselinesDirectory);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[VisualRegression] ベースラインを更新しました。 capture={captureName} baselines={baselinesDirectory}");
        }

        private static bool TryResolveStoredDirectories(out string capturesDirectory, out string baselinesDirectory)
        {
            capturesDirectory = SessionState.GetString(LastCapturesDirectoryKey, string.Empty);
            baselinesDirectory = SessionState.GetString(LastBaselinesDirectoryKey, string.Empty);
            return Directory.Exists(capturesDirectory) && Directory.Exists(baselinesDirectory);
        }

        private static string SelectCapturesDirectory()
        {
            var initialDirectory = SessionState.GetString(LastCapturesDirectoryKey, DebugOutputPath.DirectoryPath);
            return EditorUtility.OpenFolderPanel("比較する captures ディレクトリを選択", initialDirectory, string.Empty);
        }

        private static string SelectBaselinesDirectory()
        {
            var projectRootDirectory = Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
            var defaultDirectory = Path.Combine(projectRootDirectory, DefaultBaselinesDirectoryName);
            var initialDirectory = SessionState.GetString(LastBaselinesDirectoryKey, defaultDirectory);
            return EditorUtility.OpenFolderPanel("ベースライン ディレクトリを選択", initialDirectory, string.Empty);
        }
    }
}
