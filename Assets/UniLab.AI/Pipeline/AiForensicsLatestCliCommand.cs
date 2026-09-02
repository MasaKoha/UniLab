#if UNILAB_AI_PIPELINE
using System.IO;
using UniLab.AI;
using Unity.Pipeline.Commands;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// 最新フォレンジック成果物の参照を Unity 公式 CLI へ公開します。
    /// 例外本文の先頭だけを返し、詳細ファイルの読解は呼び出し側へ残します。
    /// </summary>
    public static class AiForensicsLatestCliCommand
    {
        private const string ForensicsDirectoryName = "forensics";
        private const string ErrorFileName = "error.txt";

        /// <summary>
        /// 最新の forensics ディレクトリと error.txt の先頭 20 行を返します。
        /// </summary>
        [CliCommand("ai_forensics_latest", "最新のフォレンジック結果を返します。", Tags = new[] { "forensics" })]
        public static object GetLatest()
        {
            var forensicsRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, ForensicsDirectoryName);
            var latestDirectoryPath = AiCliCommandSupport.GetLatestDirectoryPath(forensicsRootDirectoryPath);
            var errorFilePath = string.IsNullOrEmpty(latestDirectoryPath)
                ? string.Empty
                : Path.Combine(latestDirectoryPath, ErrorFileName);
            return AiForensicsLatestResult.Create(latestDirectoryPath, AiCliCommandSupport.ReadFirstLines(errorFilePath));
        }
    }
}
#endif
