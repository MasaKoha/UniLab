using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// capture 単位で無視領域を束ね、差分の理由を画面ごとに分離できるようにする。
    /// </summary>
    [Serializable]
    public sealed class VisualRegressionIgnoreRegion
    {
        /// <summary>
        /// ベースライン画像名と結び付け、別画面への誤適用を避ける。
        /// </summary>
        public string captureName;

        /// <summary>
        /// 実運用では 0 個も許し、無視が不要な画面を特別扱いしない。
        /// </summary>
        public VisualRegressionIgnoreRect[] rects;
    }
}
