using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.Diagnostics
{
    /// <summary>
    /// 1ノード分のダンプ情報です。
    /// </summary>
    [Serializable]
    public sealed class SceneHierarchyNode
    {
        /// <summary>
        /// ノードの一意 index です。
        /// </summary>
        public int index;

        /// <summary>
        /// 親ノード index です。ルートは -1 です。
        /// </summary>
        public int parentIndex;

        /// <summary>
        /// ルートからのパスです。
        /// </summary>
        public string path;

        /// <summary>
        /// GameObject 名です。
        /// </summary>
        public string name;

        /// <summary>
        /// activeSelf の値です。
        /// </summary>
        public bool activeSelf;

        /// <summary>
        /// コンポーネント型名一覧です。
        /// </summary>
        public string[] componentTypeNames;

        /// <summary>
        /// RectTransform を持つかどうかです。
        /// </summary>
        public bool hasRectTransform;

        /// <summary>
        /// anchorMin の値です。
        /// </summary>
        public float[] anchorMin;

        /// <summary>
        /// anchorMax の値です。
        /// </summary>
        public float[] anchorMax;

        /// <summary>
        /// pivot の値です。
        /// </summary>
        public float[] pivot;

        /// <summary>
        /// anchoredPosition の値です。
        /// </summary>
        public float[] anchoredPosition;

        /// <summary>
        /// sizeDelta の値です。
        /// </summary>
        public float[] sizeDelta;

        /// <summary>
        /// ワールド矩形です。x, y, width, height の順です。
        /// </summary>
        public float[] worldRect;

        /// <summary>
        /// TextMeshProUGUI を持つかどうかです。
        /// </summary>
        public bool hasTextMeshPro;

        /// <summary>
        /// テキスト先頭のプレビューです。
        /// </summary>
        public string text;

        /// <summary>
        /// フォントサイズです。
        /// </summary>
        public float fontSize;

        /// <summary>
        /// 折り返しモードです。
        /// </summary>
        public string textWrappingMode;

        /// <summary>
        /// オーバーフローモードです。
        /// </summary>
        public string overflowMode;

        /// <summary>
        /// MonoBehaviour の結線状態です。
        /// </summary>
        public SerializedFieldWiring[] serializedFields;
    }
}
#endif
