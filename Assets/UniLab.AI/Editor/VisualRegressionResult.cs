using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// capture ごとの判定根拠を残し、差分画像を見る前に失敗理由を機械読解できるようにする。
    /// </summary>
    [Serializable]
    public sealed class VisualRegressionResult
    {
        /// <summary>
        /// どの capture の結果かを明示し、ファイル名以外でも参照できるようにする。
        /// </summary>
        public string capture;

        /// <summary>
        /// pass / fail / no-baseline / size-mismatch を固定語彙にし、集計側の分岐を単純化する。
        /// </summary>
        public string status;

        /// <summary>
        /// ベースライン不在や解像度不一致の切り分けをしやすくするため、メッセージを残す。
        /// </summary>
        public string message;

        /// <summary>
        /// 比較対象の元画像を後から開けるよう、保存先を結果へ含める。
        /// </summary>
        public string actualPath;

        /// <summary>
        /// 差分原因の追跡を速くするため、比較に使ったベースラインも記録する。
        /// </summary>
        public string baselinePath;

        /// <summary>
        /// 失敗箇所の可視化画像へ直行できるよう、出力パスを持つ。
        /// </summary>
        public string diffPath;

        /// <summary>
        /// 差分割合を数値で残し、しきい値調整の判断材料にする。
        /// </summary>
        public float differenceRatio;

        /// <summary>
        /// 実際に変化と見なした画素数を残し、割合だけでは分からない規模感を補う。
        /// </summary>
        public int changedPixelCount;

        /// <summary>
        /// 比較母数を残し、無視領域込みの割合解釈を誤らないようにする。
        /// </summary>
        public int comparedPixelCount;

        /// <summary>
        /// 無視領域が効いているかを検証できるよう、除外数を残す。
        /// </summary>
        public int ignoredPixelCount;

        /// <summary>
        /// 結果ファイルだけで検証経路を再現できるよう、必要情報をまとめて渡す。
        /// </summary>
        public VisualRegressionResult(string captureName, string resultStatus, string resultMessage, string actualImagePath, string baselineImagePath, string diffImagePath, float changedRatio, int changedPixels, int comparedPixels, int ignoredPixels)
        {
            capture = string.IsNullOrEmpty(captureName) ? string.Empty : captureName;
            status = string.IsNullOrEmpty(resultStatus) ? string.Empty : resultStatus;
            message = string.IsNullOrEmpty(resultMessage) ? string.Empty : resultMessage;
            actualPath = string.IsNullOrEmpty(actualImagePath) ? string.Empty : actualImagePath;
            baselinePath = string.IsNullOrEmpty(baselineImagePath) ? string.Empty : baselineImagePath;
            diffPath = string.IsNullOrEmpty(diffImagePath) ? string.Empty : diffImagePath;
            differenceRatio = changedRatio;
            changedPixelCount = changedPixels;
            comparedPixelCount = comparedPixels;
            ignoredPixelCount = ignoredPixels;
        }
    }
}
