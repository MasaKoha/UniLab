using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// 実時間表示など設計上どうしても揺れる領域だけを狭く除外するための矩形定義。
    /// </summary>
    [Serializable]
    public sealed class VisualRegressionIgnoreRect
    {
        /// <summary>
        /// capture と同じ原点系で位置を指定し、比較時の変換を最小化する。
        /// </summary>
        public int x;

        /// <summary>
        /// capture と同じ原点系で位置を指定し、比較時の変換を最小化する。
        /// </summary>
        public int y;

        /// <summary>
        /// 除外範囲を明示しすぎないよう、必要最小限の幅だけを切り取る。
        /// </summary>
        public int width;

        /// <summary>
        /// 除外範囲を明示しすぎないよう、必要最小限の高さだけを切り取る。
        /// </summary>
        public int height;
    }
}
