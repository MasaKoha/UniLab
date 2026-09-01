using System.IO;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.Diagnostics
{
    /// <summary>
    /// デバッグ出力先ディレクトリの解決を共通化します。
    /// </summary>
    public static class DebugOutputPath
    {
        private const string OutputDirectoryName = "DebugOutput";

        /// <summary>
        /// プロジェクトルート配下のデバッグ出力先ディレクトリパスを返します。
        /// </summary>
        public static string DirectoryPath
        {
            get
            {
                var assetsDirectoryPath = Application.dataPath;
                var projectRootDirectoryPath = Path.GetDirectoryName(assetsDirectoryPath) ?? string.Empty;
                return Path.Combine(projectRootDirectoryPath, OutputDirectoryName);
            }
        }
    }
}
#endif
