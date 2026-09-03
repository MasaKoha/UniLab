using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// DeltaBarView の描画色、基準線、外枠をまとめた不変スタイル。
    /// </summary>
    public readonly struct DeltaBarStyle
    {
        /// <summary>正方向バーの色。</summary>
        public Color PositiveColor { get; }

        /// <summary>負方向バーの色。</summary>
        public Color NegativeColor { get; }

        /// <summary>値 0 のときに見せる基準線の色。</summary>
        public Color ZeroColor { get; }

        /// <summary>バー全体の背景色。</summary>
        public Color BackgroundColor { get; }

        /// <summary>通常時の基準線の色。</summary>
        public Color BaselineColor { get; }

        /// <summary>基準線の太さ。</summary>
        public float BaselineThickness { get; }

        /// <summary>基準線の位置。</summary>
        public float BaselinePosition { get; }

        /// <summary>外枠線の色。</summary>
        public Color OutlineColor { get; }

        /// <summary>外枠線の太さ。</summary>
        public float OutlineThickness { get; }

        /// <summary>
        /// UniLab 既定の差分バー表示設定を返す。
        /// </summary>
        public static DeltaBarStyle Default =>
            new DeltaBarStyle(
                positiveColor: new Color(0.3f, 0.85f, 0.4f, 1f),
                negativeColor: new Color(1f, 0.35f, 0.35f, 1f),
                zeroColor: new Color(1f, 1f, 1f, 0.75f),
                backgroundColor: new Color(1f, 1f, 1f, 0.1f),
                baselineColor: new Color(1f, 1f, 1f, 0.45f),
                baselineThickness: 2f,
                baselinePosition: 0.5f,
                outlineColor: Color.white,
                outlineThickness: 1f);

        /// <summary>
        /// DeltaBarView の描画スタイルを生成する。
        /// </summary>
        public DeltaBarStyle(
            Color positiveColor,
            Color negativeColor,
            Color zeroColor,
            Color backgroundColor,
            Color baselineColor,
            float baselineThickness,
            float baselinePosition,
            Color outlineColor,
            float outlineThickness)
        {
            PositiveColor = positiveColor;
            NegativeColor = negativeColor;
            ZeroColor = zeroColor;
            BackgroundColor = backgroundColor;
            BaselineColor = baselineColor;
            BaselineThickness = Mathf.Max(0f, baselineThickness);
            BaselinePosition = Mathf.Clamp01(baselinePosition);
            OutlineColor = outlineColor;
            OutlineThickness = Mathf.Max(0f, outlineThickness);
        }
    }
}
