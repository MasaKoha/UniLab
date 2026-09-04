#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>AI クライアント（CLI / メールボックス）から届く 1 要求です。</summary>
    [Serializable]
    public sealed class AiCommandRequest
    {
        /// <summary>実行する操作名です。</summary>
        public string op;
        /// <summary>引数を格納した JSON オブジェクト文字列です。</summary>
        public string args;
    }
}
#endif
