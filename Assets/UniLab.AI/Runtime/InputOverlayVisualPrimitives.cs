#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AI
{
    /// <summary>機器描画と履歴帯に共通する UI 生成を保持します。</summary>
    internal static class InputOverlayVisualPrimitives
    {
        private static Texture2D s_circleTexture;

        private static Sprite s_whiteSprite;

        private static Sprite s_circleSprite;

        /// <summary>子要素の座標系を従来の全画面ルートへ揃えます。</summary>
        internal static RectTransform CreateContainer(string name, RectTransform parent)
        {
            var containerObject = new GameObject(name, typeof(RectTransform));
            containerObject.transform.SetParent(parent, false);
            var rectTransform = containerObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return rectTransform;
        }

        /// <summary>背景とサイズを同じ生成経路で設定します。</summary>
        internal static Image CreatePanel(string name, RectTransform parent, Vector2 size, Color color)
        {
            var image = CreateImage(name, parent, Texture2D.whiteTexture, color);
            image.rectTransform.sizeDelta = size;
            return image;
        }

        /// <summary>画像生成時のスプライト再利用を維持します。</summary>
        internal static Image CreateImage(string name, RectTransform parent, Texture2D texture, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.sprite = GetSprite(texture);
            image.color = color;
            return image;
        }

        /// <summary>フォントと文字組みの既定を描画間で揃えます。</summary>
        internal static TextMeshProUGUI CreateText(string name, RectTransform parent, float fontSize, TextAlignmentOptions alignment, FontStyles fontStyle)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.enableWordWrapping = false;
            return text;
        }

        /// <summary>親の矩形へ追従する配置規則を維持します。</summary>
        internal static void Stretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        /// <summary>画面座標をそのまま使えるアンカーへ揃えます。</summary>
        internal static void SetBottomLeftAnchor(RectTransform rectTransform, Vector2 pivot)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = pivot;
        }

        /// <summary>設定された隅からの余白を統一します。</summary>
        internal static void AnchorToCorner(RectTransform rectTransform, OverlayCorner corner, float marginX, float marginY)
        {
            switch (corner)
            {
                case OverlayCorner.TopLeft:
                    rectTransform.anchorMin = new Vector2(0f, 1f);
                    rectTransform.anchorMax = new Vector2(0f, 1f);
                    rectTransform.pivot = new Vector2(0f, 1f);
                    rectTransform.anchoredPosition = new Vector2(marginX, -marginY);
                    break;
                case OverlayCorner.TopRight:
                    rectTransform.anchorMin = new Vector2(1f, 1f);
                    rectTransform.anchorMax = new Vector2(1f, 1f);
                    rectTransform.pivot = new Vector2(1f, 1f);
                    rectTransform.anchoredPosition = new Vector2(-marginX, -marginY);
                    break;
                case OverlayCorner.BottomLeft:
                    rectTransform.anchorMin = new Vector2(0f, 0f);
                    rectTransform.anchorMax = new Vector2(0f, 0f);
                    rectTransform.pivot = new Vector2(0f, 0f);
                    rectTransform.anchoredPosition = new Vector2(marginX, marginY);
                    break;
                default:
                    rectTransform.anchorMin = new Vector2(1f, 0f);
                    rectTransform.anchorMax = new Vector2(1f, 0f);
                    rectTransform.pivot = new Vector2(1f, 0f);
                    rectTransform.anchoredPosition = new Vector2(-marginX, marginY);
                    break;
            }
        }

        /// <summary>波紋とスティックで同じ円形テクスチャを再利用します。</summary>
        internal static Texture2D GetCircleTexture()
        {
            if (s_circleTexture != null)
            {
                return s_circleTexture;
            }

            const int textureSize = 64;
            s_circleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            s_circleTexture.wrapMode = TextureWrapMode.Clamp;
            var center = (textureSize - 1) * 0.5f;
            var radius = center;
            for (var y = 0; y < textureSize; y++)
            {
                for (var x = 0; x < textureSize; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var normalized = Mathf.Clamp01(1f - ((distance - (radius - 2f)) / 2f));
                    var color = new Color(1f, 1f, 1f, normalized);
                    s_circleTexture.SetPixel(x, y, color);
                }
            }

            s_circleTexture.Apply();
            return s_circleTexture;
        }

        /// <summary>既存のスプライトキャッシュを使い描画資産の重複生成を避けます。</summary>
        internal static Sprite GetSprite(Texture2D texture)
        {
            if (texture == Texture2D.whiteTexture)
            {
                if (s_whiteSprite == null)
                {
                    s_whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }

                return s_whiteSprite;
            }

            if (texture == GetCircleTexture())
            {
                if (s_circleSprite == null)
                {
                    s_circleSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }

                return s_circleSprite;
            }

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>履歴とポインタで色指定の解決規則を揃えます。</summary>
        internal static Color ParseHtmlColor(string htmlColor)
        {
            if (ColorUtility.TryParseHtmlString(htmlColor, out var color))
            {
                return color;
            }

            return Color.white;
        }
    }
}
#endif
