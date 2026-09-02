using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// ギャラリー側がディレクトリ総走査なしでラン一覧を描画できるよう、要約済みの索引を固定化する。
    /// </summary>
    [Serializable]
    public sealed class RunArchiveIndex
    {
        /// <summary>
        /// 古い索引かどうかを判定しやすいよう、再生成時刻を保持する。
        /// </summary>
        public string generatedAt;

        /// <summary>
        /// ラン一覧描画に必要な最小情報だけを平坦化し、スマホ側の処理を単純化する。
        /// </summary>
        public RunArchiveIndexEntry[] runs;

        /// <summary>
        /// 毎回 null 埋めを考えずに済むよう、索引の既定値を構築時に固定する。
        /// </summary>
        public RunArchiveIndex(string generatedAtText, RunArchiveIndexEntry[] entries)
        {
            generatedAt = string.IsNullOrEmpty(generatedAtText) ? string.Empty : generatedAtText;
            runs = entries ?? Array.Empty<RunArchiveIndexEntry>();
        }
    }
}
