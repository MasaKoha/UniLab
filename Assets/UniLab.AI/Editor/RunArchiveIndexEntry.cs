using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// 1 ランぶんの導線を索引へ切り出し、詳細ファイルを開く前の一覧判断を高速化する。
    /// </summary>
    [Serializable]
    public sealed class RunArchiveIndexEntry
    {
        /// <summary>
        /// ディレクトリ名をそのまま持ち、URL 生成や削除対象指定を安定させる。
        /// </summary>
        public string runId;

        /// <summary>
        /// ランディレクトリへの相対パスを保持し、配信ルート配下で安全に解決できるようにする。
        /// </summary>
        public string path;

        /// <summary>
        /// 要約ファイルへの導線を固定し、詳細読込時の探索を不要にする。
        /// </summary>
        public string metaPath;

        /// <summary>
        /// 一覧上で検証意図を識別できるよう、シナリオ名を保持する。
        /// </summary>
        public string scenario;

        /// <summary>
        /// 一覧の色分け判定を単純化するため、合否を文字列で持つ。
        /// </summary>
        public string verdict;

        /// <summary>
        /// 並び順の説明責務を索引自体が持てるよう、開始時刻を保持する。
        /// </summary>
        public string startedAt;

        /// <summary>
        /// 長時間ハングの切り分けを一覧だけで始められるよう、終了時刻も保持する。
        /// </summary>
        public string finishedAt;

        /// <summary>
        /// 一覧比較でラン長の異常をすぐ見つけられるよう、秒数を含める。
        /// </summary>
        public float durationSeconds;

        /// <summary>
        /// 索引更新時のロジックを単純化するため、一覧表示の最小単位を一度に構築できるようにする。
        /// </summary>
        public RunArchiveIndexEntry(string runIdentifier, string relativePath, string relativeMetaPath, string scenarioName, string verdictText, string startedAtText, string finishedAtText, float durationSecondsValue)
        {
            runId = string.IsNullOrEmpty(runIdentifier) ? string.Empty : runIdentifier;
            path = string.IsNullOrEmpty(relativePath) ? string.Empty : relativePath;
            metaPath = string.IsNullOrEmpty(relativeMetaPath) ? string.Empty : relativeMetaPath;
            scenario = string.IsNullOrEmpty(scenarioName) ? string.Empty : scenarioName;
            verdict = string.IsNullOrEmpty(verdictText) ? string.Empty : verdictText;
            startedAt = string.IsNullOrEmpty(startedAtText) ? string.Empty : startedAtText;
            finishedAt = string.IsNullOrEmpty(finishedAtText) ? string.Empty : finishedAtText;
            durationSeconds = durationSecondsValue;
        }
    }
}
