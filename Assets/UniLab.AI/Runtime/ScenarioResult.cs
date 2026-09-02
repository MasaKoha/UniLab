#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// シナリオ全体の合否と成果物パスを固定形式で返し、外部ブリッジが完了をファイルで判断できるようにします。
    /// </summary>
    [Serializable]
    public sealed class ScenarioResult
    {
        /// <summary>
        /// シナリオ名です。
        /// </summary>
        public string scenario;

        /// <summary>
        /// pass / fail / error の最終判定です。
        /// </summary>
        public string verdict;

        /// <summary>
        /// 開始時刻です。
        /// </summary>
        public string startedAt;

        /// <summary>
        /// 終了時刻です。
        /// </summary>
        public string finishedAt;

        /// <summary>
        /// 実行時間を結果単体で読めるようにします。
        /// </summary>
        public float durationSeconds;

        /// <summary>
        /// 実行対象として読み込んだ総ステップ数です。
        /// </summary>
        public int stepCount;

        /// <summary>
        /// 合格したステップ数です。
        /// </summary>
        public int passedSteps;

        /// <summary>
        /// 失敗したステップ数です。
        /// </summary>
        public int failedSteps;

        /// <summary>
        /// フォレンジックが捕捉した例外・エラーログ数です。
        /// </summary>
        public int exceptionCount;

        /// <summary>
        /// フォレンジックが洪水抑制や重複抑制で保存しなかった件数です。
        /// </summary>
        public int exceptionSuppressedCount;

        /// <summary>
        /// ランナーが警告として検知した異常数です。
        /// </summary>
        public int warningCount;

        /// <summary>
        /// 録画が落としたフレーム数です。
        /// </summary>
        public int droppedFrameCount;

        /// <summary>
        /// ステップ単位の合否です。
        /// </summary>
        public ScenarioStepResult[] steps;

        /// <summary>
        /// 録画成果物ディレクトリの一覧です。
        /// </summary>
        public string[] recordings;

        /// <summary>
        /// 例外フォレンジックの出力ディレクトリ一覧です。
        /// </summary>
        public string[] exceptions;

        /// <summary>
        /// 性能計測レポートのパスです。
        /// </summary>
        public string performance;

        /// <summary>
        /// 視覚回帰レポートの受け渡し欄です。
        /// </summary>
        public string visualRegression;
    }
}
#endif
