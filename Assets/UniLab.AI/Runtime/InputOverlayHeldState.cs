#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AI
{
    /// <summary>単一入力の押下・解放時刻と保持表示の状態を保持します。</summary>
    internal sealed class InputOverlayHeldState
    {
        private const float HoldFadeSeconds = 0.2f;

        /// <summary>未押下の初期状態を揃え、初回入力を反復扱いしないようにします。</summary>
        public InputOverlayHeldState(string baseLabel)
        {
            this.baseLabel = baseLabel;
            repeatCount = 1;
            lastReleasedAt = -1f;
        }

        /// <summary>反復回数を付ける前のラベルを保持します。</summary>
        public readonly string baseLabel;
        /// <summary>マウス模式図のハイライトを同じ押下状態へ結び付けます。</summary>
        public Image background;
        /// <summary>マウス模式図の反復回数を同じ押下状態から表示します。</summary>
        public TextMeshProUGUI label;
        /// <summary>保持表示中と実際の押下中を区別します。</summary>
        public bool isPressed;
        /// <summary>保持表示が続く間の再押下回数を保持します。</summary>
        public int repeatCount;
        /// <summary>押下開始の時刻を後続の表示へ引き継ぎます。</summary>
        public float lastPressedAt;
        /// <summary>解放後の保持とフェードの起点です。</summary>
        public float lastReleasedAt;
        /// <summary>最近操作したキーを優先表示するための時刻です。</summary>
        public float lastVisibleAt;

        /// <summary>状態を作り直さずに、生成した模式図へ接続します。</summary>
        public void BindVisual(Image backgroundImage, TextMeshProUGUI labelText)
        {
            background = backgroundImage;
            label = labelText;
        }

        /// <summary>短い入力も録画で読み取れるよう、解放後の表示を保持します。</summary>
        public bool IsVisible(float now, float holdSeconds)
        {
            if (isPressed)
            {
                return true;
            }

            if (lastReleasedAt < 0f)
            {
                return false;
            }

            return now - lastReleasedAt < holdSeconds + HoldFadeSeconds;
        }

        /// <summary>解放後の保持時間とフェード時間を同じ規則で描画へ渡します。</summary>
        public float GetAlpha(float now, float holdSeconds)
        {
            if (isPressed)
            {
                return 1f;
            }

            if (lastReleasedAt < 0f)
            {
                return 0f;
            }

            var clampedHoldSeconds = Mathf.Max(0.01f, holdSeconds);
            var elapsed = now - lastReleasedAt;
            if (elapsed <= clampedHoldSeconds)
            {
                return 1f;
            }

            var fadeElapsed = elapsed - clampedHoldSeconds;
            if (fadeElapsed >= HoldFadeSeconds)
            {
                return 0f;
            }

            return 1f - (fadeElapsed / HoldFadeSeconds);
        }

        /// <summary>再押下の回数をラベルに残し、連打を動画から判別できるようにします。</summary>
        public string GetDisplayText()
        {
            if (repeatCount <= 1)
            {
                return baseLabel;
            }

            return $"{baseLabel} ×{repeatCount}";
        }
    }
}
#endif
