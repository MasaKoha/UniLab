namespace UniLab.UI.Tween
{
    /// <summary>
    /// UiTween で利用するイージング種別。DOTween 非依存で UI アニメーションを表現するために定義する。
    /// </summary>
    public enum EaseType
    {
        /// <summary>等速。</summary>
        Linear,

        /// <summary>加速（始点で緩やか）。</summary>
        InQuad,

        /// <summary>減速（終点で緩やか）。</summary>
        OutQuad,

        /// <summary>行き過ぎてから戻る（始点側でバックする）。閉じる演出向け。</summary>
        InBack,

        /// <summary>勢いよく出て少し行き過ぎてから収まる。開く演出向け。</summary>
        OutBack,
    }
}
