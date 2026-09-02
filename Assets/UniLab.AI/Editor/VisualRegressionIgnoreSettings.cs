using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// `ignore.json` の内容を配列へ正規化し、`JsonUtility` と同じ扱いやすさで後続処理へ渡す。
    /// </summary>
    [Serializable]
    public sealed class VisualRegressionIgnoreSettings
    {
        /// <summary>
        /// capture 名ごとの矩形配列を保持し、検索責務を比較本体へ持ち込まないようにする。
        /// </summary>
        public VisualRegressionIgnoreRegion[] captures;
    }
}
