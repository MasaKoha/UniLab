using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// SegmentedBarView の描画色と線幅、並び方向をまとめた不変スタイル。
    /// </summary>
    public readonly struct SegmentedBarStyle
    {
        /// <summary>塗りの単色。</summary>
        public Color FillColor { get; }

        /// <summary>塗りの左端色。</summary>
        public Color FillStartColor { get; }

        /// <summary>塗りの右端色。</summary>
        public Color FillEndColor { get; }

        /// <summary>未充填セグメントの色。</summary>
        public Color BackgroundColor { get; }

        /// <summary>セグメント境界線の色。</summary>
        public Color SeparatorColor { get; }

        /// <summary>セグメント境界線の太さ。</summary>
        public float SeparatorThickness { get; }

        /// <summary>外枠線の色。</summary>
        public Color OutlineColor { get; }

        /// <summary>外枠線の太さ。</summary>
        public float OutlineThickness { get; }

        /// <summary>セグメント間の隙間。</summary>
        public float SegmentSpacing { get; }

        /// <summary>縦方向に積むか。</summary>
        public bool Vertical { get; }

        /// <summary>
        /// UniLab 既定の区切りバー表示設定を返す。
        /// </summary>
        public static SegmentedBarStyle Default =>
            new SegmentedBarStyle(
                fillColor: new Color(0.2f, 0.7f, 1f, 1f),
                fillStartColor: Color.clear,
                fillEndColor: Color.clear,
                backgroundColor: new Color(1f, 1f, 1f, 0.12f),
                separatorColor: new Color(1f, 1f, 1f, 0.5f),
                separatorThickness: 1f,
                outlineColor: Color.white,
                outlineThickness: 1f,
                segmentSpacing: 2f,
                vertical: false);

        /// <summary>
        /// SegmentedBarView の描画スタイルを生成する。
        /// </summary>
        public SegmentedBarStyle(
            Color fillColor,
            Color fillStartColor,
            Color fillEndColor,
            Color backgroundColor,
            Color separatorColor,
            float separatorThickness,
            Color outlineColor,
            float outlineThickness,
            float segmentSpacing,
            bool vertical = false)
        {
            FillColor = fillColor;
            FillStartColor = fillStartColor;
            FillEndColor = fillEndColor;
            BackgroundColor = backgroundColor;
            SeparatorColor = separatorColor;
            SeparatorThickness = Mathf.Max(0f, separatorThickness);
            OutlineColor = outlineColor;
            OutlineThickness = Mathf.Max(0f, outlineThickness);
            SegmentSpacing = Mathf.Max(0f, segmentSpacing);
            Vertical = vertical;
        }
    }
}
