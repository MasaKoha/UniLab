using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// ノイズ許容を明示設定に切り出し、UI 特性ごとの差分感度をコード変更なしで調整できるようにする。
    /// </summary>
    [Serializable]
    public sealed class VisualRegressionOptions
    {
        /// <summary>
        /// アンチエイリアス由来の微差を吸収するため、既定で 1/2 縮小する。
        /// </summary>
        public int downscaleDivisor = 2;

        /// <summary>
        /// 文字周りの揺れを許しつつ明確な崩れは拾うため、既定しきい値を 24 にする。
        /// </summary>
        public byte differenceThreshold = 24;

        /// <summary>
        /// 画面全体に対する微小差分で失敗しないよう、既定許容率を 0.5% にする。
        /// </summary>
        public float allowedDifferenceRatio = 0.005f;

        /// <summary>
        /// 差分画像で元絵の位置関係を読めるよう、非差分画素は半透明で残す。
        /// </summary>
        public byte unchangedAlpha = 96;
    }
}
