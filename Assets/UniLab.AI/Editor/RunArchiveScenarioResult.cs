using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// シナリオ結果 JSON を再保存可能な形で受け、RunArchive 内で相対パスへ正規化できるようにする。
    /// </summary>
    [Serializable]
    public sealed class RunArchiveScenarioResult
    {
        /// <summary>
        /// ラン要約の主キーになるため、シナリオ名を保持する。
        /// </summary>
        public string scenario;

        /// <summary>
        /// 一覧色分けの基準にするため、ラン全体の判定を保持する。
        /// </summary>
        public string verdict;

        /// <summary>
        /// 他成果物の抽出窓を決めるため、開始時刻を保持する。
        /// </summary>
        public string startedAt;

        /// <summary>
        /// 抽出窓の終端とラン長の基準にするため、終了時刻を保持する。
        /// </summary>
        public string finishedAt;

        /// <summary>
        /// 要約生成で再計算せず使えるよう、実行秒数も保持する。
        /// </summary>
        public float durationSeconds;

        /// <summary>
        /// 結果の十分性を一覧で判断できるよう、総ステップ数を維持する。
        /// </summary>
        public int stepCount;

        /// <summary>
        /// 合格数を一覧で見えるようにし、失敗 0 以外の異常をすぐ拾えるようにする。
        /// </summary>
        public int passedSteps;

        /// <summary>
        /// 失敗数をメタ要約なしでも読めるようにする。
        /// </summary>
        public int failedSteps;

        /// <summary>
        /// フォレンジック件数との整合確認に使うため、例外件数を保持する。
        /// </summary>
        public int exceptionCount;

        /// <summary>
        /// 警告多発ランを一覧で見分けるため、警告件数も保持する。
        /// </summary>
        public int warningCount;

        /// <summary>
        /// 録画負荷の影響をラン結果と一緒に残すため、取りこぼし数を保持する。
        /// </summary>
        public int droppedFrameCount;

        /// <summary>
        /// 失敗証拠のパスを書き換えても結果本文を保てるよう、各ステップ配列を維持する。
        /// </summary>
        public RunArchiveScenarioStepResult[] steps;

        /// <summary>
        /// 録画ディレクトリを相対化してギャラリーから辿れるようにする。
        /// </summary>
        public string[] recordings;

        /// <summary>
        /// フォレンジックディレクトリを相対化してラン配下へ閉じ込めるため保持する。
        /// </summary>
        public string[] exceptions;
    }
}
