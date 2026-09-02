#if UNILAB_AI_PIPELINE
using System;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// `ai_forensics_latest` の返却形式です。
    /// 最新ディレクトリ参照と短い本文プレビューを同じ応答へまとめます。
    /// </summary>
    [Serializable]
    internal sealed class AiForensicsLatestResult
    {
        public string path;
        public string[] errorPreviewLines;

        internal static AiForensicsLatestResult Create(string path, string[] errorPreviewLines)
        {
            return new AiForensicsLatestResult
            {
                path = path ?? string.Empty,
                errorPreviewLines = errorPreviewLines ?? Array.Empty<string>(),
            };
        }
    }
}
#endif
