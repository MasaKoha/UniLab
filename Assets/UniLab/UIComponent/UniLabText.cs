using TMPro;
using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// プロジェクト共通の TextMeshProUGUI 基底コンポーネント。単体でもそのまま使える。
    /// OnEnable / OnValidate で <see cref="ApplyStyle"/> を呼ぶ共通フックを提供し、
    /// デザイントークン適用やフォント/フォールバック統一の実装を派生クラスへ集約させる。
    /// 既定の <see cref="ApplyStyle"/> は何もしない（素の TMP として振る舞う）。
    /// </summary>
    [AddComponentMenu("UniLab/UI/UniLab Text")]
    public class UniLabText : TextMeshProUGUI
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyStyle();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyStyle();
        }
#endif

        /// <summary>
        /// スタイル（サイズ・色・フォント等）を適用する。派生クラスで上書きする。
        /// font 未設定時に値を入れると TMP が NRE するため、実装側で font の null を考慮すること。
        /// </summary>
        protected virtual void ApplyStyle()
        {
        }
    }
}
