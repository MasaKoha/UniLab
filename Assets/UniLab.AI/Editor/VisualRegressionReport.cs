using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// ラン全体の比較結果を 1 ファイルへ集約し、RunArchive などが後処理しやすい形を作る。
    /// </summary>
    [Serializable]
    public sealed class VisualRegressionReport
    {
        /// <summary>
        /// どの実画像ディレクトリを比較したかを残し、Accept の再実行元を辿れるようにする。
        /// </summary>
        public string capturesDirectory;

        /// <summary>
        /// ベースライン更新時の事故を避けるため、比較先の場所もレポートへ固定する。
        /// </summary>
        public string baselinesDirectory;

        /// <summary>
        /// 差分画像群の保存先を残し、相対参照を組み立てずに開けるようにする。
        /// </summary>
        public string outputDirectory;

        /// <summary>
        /// 比較実行時刻を残し、複数ランの時系列を復元できるようにする。
        /// </summary>
        public string generatedAt;

        /// <summary>
        /// capture ごとの詳細結果を保持し、失敗理由を画面単位で確認できるようにする。
        /// </summary>
        public VisualRegressionResult[] results;

        /// <summary>
        /// 目視不要で成功数を把握できるよう、集計値も同梱する。
        /// </summary>
        public int passCount;

        /// <summary>
        /// 目視不要で失敗数を把握できるよう、集計値も同梱する。
        /// </summary>
        public int failCount;

        /// <summary>
        /// 初回導入の未整備を失敗と分離するため、no-baseline 件数を別立てにする。
        /// </summary>
        public int noBaselineCount;

        /// <summary>
        /// 解像度差は画素差と原因が違うため、別件数にして扱いを分ける。
        /// </summary>
        public int sizeMismatchCount;

        /// <summary>
        /// 後続集計が単純化するよう、件数を構築時に確定しておく。
        /// </summary>
        public VisualRegressionReport(string actualDirectoryPath, string baselineDirectoryPath, string resultOutputDirectory, string generatedAtText, VisualRegressionResult[] comparisonResults, int passedCount, int failedCount, int missingBaselineCount, int mismatchedSizeCount)
        {
            capturesDirectory = string.IsNullOrEmpty(actualDirectoryPath) ? string.Empty : actualDirectoryPath;
            baselinesDirectory = string.IsNullOrEmpty(baselineDirectoryPath) ? string.Empty : baselineDirectoryPath;
            outputDirectory = string.IsNullOrEmpty(resultOutputDirectory) ? string.Empty : resultOutputDirectory;
            generatedAt = string.IsNullOrEmpty(generatedAtText) ? string.Empty : generatedAtText;
            results = comparisonResults ?? Array.Empty<VisualRegressionResult>();
            passCount = passedCount;
            failCount = failedCount;
            noBaselineCount = missingBaselineCount;
            sizeMismatchCount = mismatchedSizeCount;
        }
    }
}
