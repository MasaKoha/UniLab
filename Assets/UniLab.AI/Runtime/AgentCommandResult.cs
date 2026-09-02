#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 静的ブリッジ呼び出しの返り値を常に JSON 化し、外側が文字列解析に頼らないようにします。
    /// </summary>
    [Serializable]
    public sealed class AgentCommandResult
    {
        /// <summary>呼び出しが受理されたかどうかです。</summary>
        public bool ok;

        /// <summary>現在のセッション識別子です。</summary>
        public string session;

        /// <summary>人間と LLM の両方が読むための短い結果説明です。</summary>
        public string message;

        /// <summary>観測テキストまたは実行結果テキストです。</summary>
        public string text;

        /// <summary>成果物ファイルへのパスです。</summary>
        public string path;
    }
}
#endif
