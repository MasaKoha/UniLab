#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>観測本文と成果物を共通の形で返す要求結果です。</summary>
    [Serializable]
    public sealed class AiCommandResponse
    {
        /// <summary>操作が正常に処理されたかを示します。</summary>
        public bool ok;
        /// <summary>要求された操作名です。</summary>
        public string op = string.Empty;
        /// <summary>セッション識別子です。</summary>
        public string session = string.Empty;
        /// <summary>短い結果説明です。</summary>
        public string message = string.Empty;
        /// <summary>観測または本文です。</summary>
        public string text = string.Empty;
        /// <summary>成果物のパスです。</summary>
        public string path = string.Empty;
        /// <summary>非同期経路で落ち着き待ち、または撮影完了を確認した場合に true です。</summary>
        public bool settled;
        /// <summary>失敗の理由です。</summary>
        public string error = string.Empty;
    }
}
#endif
