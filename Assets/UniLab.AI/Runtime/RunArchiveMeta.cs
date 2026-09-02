#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// ラン全体の要約を 1 ファイルへ固定し、スマホ閲覧や後続集計が個別成果物を毎回走査しなくて済むようにする。
    /// </summary>
    [Serializable]
    public sealed class RunArchiveMeta
    {
        /// <summary>
        /// どのシナリオの検証結果かを一覧で識別できるように保持する。
        /// </summary>
        public string scenario;

        /// <summary>
        /// 失敗ランを一覧の段階で見分けられるよう、ラン全体の判定を平坦化して持つ。
        /// </summary>
        public string verdict;

        /// <summary>
        /// 他成果物の時刻と照合しやすいよう、開始時刻を保持する。
        /// </summary>
        public string startedAt;

        /// <summary>
        /// 複数ランの前後関係を復元できるよう、終了時刻も保持する。
        /// </summary>
        public string finishedAt;

        /// <summary>
        /// 一覧比較で長さの異常をすぐ見つけられるよう、秒数を要約へ含める。
        /// </summary>
        public float durationSeconds;

        /// <summary>
        /// 目視の手がかり量を見積もれるよう、通常キャプチャ枚数を残す。
        /// </summary>
        public int captures;

        /// <summary>
        /// 監査実行回数を一覧で比較できるよう、件数を固定する。
        /// </summary>
        public int audits;

        /// <summary>
        /// 監査の総検出数を要約へ持ち上げ、詳細 JSON を開く前に異常量を把握できるようにする。
        /// </summary>
        public int auditFindingsTotal;

        /// <summary>
        /// シナリオ結果とフォレンジックの突き合わせを 1 値で始められるよう、例外件数を要約へ置く。
        /// </summary>
        public int exceptions;

        /// <summary>
        /// 送出見送りや警告増加を一覧で把握できるようにする。
        /// </summary>
        public int warnings;

        /// <summary>
        /// 録画負荷による取りこぼしを一覧で比較できるよう、動画 manifest の合算値を保持する。
        /// </summary>
        public int droppedFrames;

        /// <summary>
        /// 録画名だけ先に見えれば動画の場所を詳細 JSON なしで推測できるため、名前配列で持つ。
        /// </summary>
        public string[] recordings;

        /// <summary>
        /// 視覚回帰の集計を埋め込み、失敗ラン一覧から差分有無を直ちに判断できるようにする。
        /// </summary>
        public RunArchiveVisualRegressionSummary visualRegression;

        /// <summary>
        /// 性能の主要指標だけを埋め込み、詳細レポートを開く対象を先に絞れるようにする。
        /// </summary>
        public RunArchivePerformanceSummary performance;

        /// <summary>
        /// ランと修正差分を後から結び直せるよう、取得できたコミットだけを保持する。
        /// </summary>
        public string gitCommit;

        /// <summary>
        /// Unity 更新による差分をラン一覧から切り分けられるよう、実行バージョンを残す。
        /// </summary>
        public string unityVersion;

        /// <summary>
        /// 後続処理が null 分岐だらけにならないよう、要約を構築時点で既定値込みで確定させる。
        /// </summary>
        public RunArchiveMeta(
            string scenarioName,
            string verdictText,
            string startedAtText,
            string finishedAtText,
            float durationSecondsValue,
            int captureCount,
            int auditCount,
            int auditFindingCount,
            int exceptionCount,
            int warningCount,
            int droppedFrameCount,
            string[] recordingNames,
            RunArchiveVisualRegressionSummary visualRegressionSummary,
            RunArchivePerformanceSummary performanceSummary,
            string gitCommitHash,
            string unityVersionText)
        {
            scenario = string.IsNullOrEmpty(scenarioName) ? string.Empty : scenarioName;
            verdict = string.IsNullOrEmpty(verdictText) ? string.Empty : verdictText;
            startedAt = string.IsNullOrEmpty(startedAtText) ? string.Empty : startedAtText;
            finishedAt = string.IsNullOrEmpty(finishedAtText) ? string.Empty : finishedAtText;
            durationSeconds = durationSecondsValue;
            captures = captureCount;
            audits = auditCount;
            auditFindingsTotal = auditFindingCount;
            exceptions = exceptionCount;
            warnings = warningCount;
            droppedFrames = droppedFrameCount;
            recordings = recordingNames ?? Array.Empty<string>();
            visualRegression = visualRegressionSummary ?? new RunArchiveVisualRegressionSummary(0, 0, 0);
            performance = performanceSummary ?? new RunArchivePerformanceSummary(0.0f);
            gitCommit = string.IsNullOrEmpty(gitCommitHash) ? string.Empty : gitCommitHash;
            unityVersion = string.IsNullOrEmpty(unityVersionText) ? string.Empty : unityVersionText;
        }
    }
}
#endif
