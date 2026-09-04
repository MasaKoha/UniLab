#if UNILAB_AI_PIPELINE
using System;

namespace UniLab.AI.Pipeline
{
    /// <summary>CLI の型付き引数を JSON にエスケープするための転送形式です。</summary>
    [Serializable]
    internal sealed class AiCliArguments
    {
        /// <summary>成果物名です。</summary>
        public string name;
        /// <summary>保存先です。</summary>
        public string directory;
        /// <summary>差分観測を指定します。</summary>
        public bool diffOnly;
        /// <summary>圧縮形式を指定します。</summary>
        public bool compact;
        /// <summary>スナップショットの保存を指定します。</summary>
        public bool save;
    }
}
#endif
