using UnityEngine;

namespace UniLab.UI.Tween
{
    /// <summary>
    /// 正規化された進捗 t(0..1) にイージングを適用する純粋関数群。
    /// DOTween を排し、共通ライブラリ内でアニメーション曲線を自前提供するために用意する。
    /// </summary>
    public static class Easing
    {
        // Back 系の戻り量を決める係数。DOTween / Robert Penner 実装と同じ既定値を採用する。
        private const float BackConstant = 1.70158f;
        private const float BackConstantPlusOne = BackConstant + 1f;

        /// <summary>
        /// 指定のイージングを進捗 t に適用した値を返す。呼び出し側で Lerp の係数として使う。
        /// </summary>
        public static float Evaluate(EaseType easeType, float t)
        {
            switch (easeType)
            {
                case EaseType.InQuad:
                {
                    return t * t;
                }
                case EaseType.OutQuad:
                {
                    return 1f - (1f - t) * (1f - t);
                }
                case EaseType.InBack:
                {
                    return BackConstantPlusOne * t * t * t - BackConstant * t * t;
                }
                case EaseType.OutBack:
                {
                    var inverted = t - 1f;
                    return 1f + BackConstantPlusOne * inverted * inverted * inverted + BackConstant * inverted * inverted;
                }
                case EaseType.Linear:
                default:
                {
                    return t;
                }
            }
        }
    }
}
