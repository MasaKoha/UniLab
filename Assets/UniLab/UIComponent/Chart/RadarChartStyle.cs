using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// RadarChartView の描画色・線幅・角度方向をまとめた不変スタイル。
    /// </summary>
    public readonly struct RadarChartStyle
    {
        /// <summary>外枠線の色。</summary>
        public Color OutlineColor { get; }

        /// <summary>外枠線の太さ。</summary>
        public float OutlineThickness { get; }

        /// <summary>軸線の色。</summary>
        public Color AxisLineColor { get; }

        /// <summary>軸線の太さ。</summary>
        public float AxisLineThickness { get; }

        /// <summary>値多角形の塗り色。</summary>
        public Color FillColor { get; }

        /// <summary>値多角形の中心色。</summary>
        public Color FillCenterColor { get; }

        /// <summary>値多角形の外周色。</summary>
        public Color FillEdgeColor { get; }

        /// <summary>値多角形の縁色。</summary>
        public Color ValueOutlineColor { get; }

        /// <summary>値多角形の縁の太さ。</summary>
        public float ValueOutlineThickness { get; }

        /// <summary>背景多角形の色。</summary>
        public Color BackgroundColor { get; }

        /// <summary>先頭頂点の開始角度。90 度で真上。</summary>
        public float StartAngleDegrees { get; }

        /// <summary>頂点を時計回りに並べるか。</summary>
        public bool Clockwise { get; }

        /// <summary>
        /// UniLab 既定のレーダーチャート表示設定を返す。
        /// </summary>
        public static RadarChartStyle Default =>
            new RadarChartStyle(
                outlineColor: Color.white,
                outlineThickness: 2f,
                axisLineColor: new Color(1f, 1f, 1f, 0.4f),
                axisLineThickness: 1f,
                fillColor: new Color(0.2f, 0.7f, 1f, 0.3f),
                fillCenterColor: Color.clear,
                fillEdgeColor: Color.clear,
                valueOutlineColor: new Color(0.2f, 0.7f, 1f, 1f),
                valueOutlineThickness: 2f,
                backgroundColor: new Color(1f, 1f, 1f, 0.08f));

        /// <summary>
        /// RadarChartView の描画スタイルを生成する。
        /// </summary>
        public RadarChartStyle(
            Color outlineColor,
            float outlineThickness,
            Color axisLineColor,
            float axisLineThickness,
            Color fillColor,
            Color fillCenterColor,
            Color fillEdgeColor,
            Color valueOutlineColor,
            float valueOutlineThickness,
            Color backgroundColor,
            float startAngleDegrees = 90f,
            bool clockwise = true)
        {
            OutlineColor = outlineColor;
            OutlineThickness = Mathf.Max(0f, outlineThickness);
            AxisLineColor = axisLineColor;
            AxisLineThickness = Mathf.Max(0f, axisLineThickness);
            FillColor = fillColor;
            FillCenterColor = fillCenterColor;
            FillEdgeColor = fillEdgeColor;
            ValueOutlineColor = valueOutlineColor;
            ValueOutlineThickness = Mathf.Max(0f, valueOutlineThickness);
            BackgroundColor = backgroundColor;
            StartAngleDegrees = startAngleDegrees;
            Clockwise = clockwise;
        }
    }
}
