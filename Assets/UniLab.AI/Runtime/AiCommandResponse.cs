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
        /// <summary>非同期撮影で読み取った画像の幅です。同期経路では 0 です。</summary>
        public int width;
        /// <summary>非同期撮影で読み取った画像の高さです。同期経路では 0 です。</summary>
        public int height;
        /// <summary>輝度の標準偏差が閾値未満の画像です。同期経路では false です。</summary>
        public bool blank;
        /// <summary>非同期経路で落ち着き待ち、または撮影完了を確認した場合に true です。</summary>
        public bool settled;
        /// <summary>非同期の最終行動で対象が操作可能になった場合に true です。</summary>
        public bool ready;
        /// <summary>最終行動の準備待ちに費やした実時間のミリ秒です。</summary>
        public int waitedMs;
        /// <summary>要求の実行開始から応答完成までの実時間のミリ秒です。</summary>
        public int elapsedMs;
        /// <summary>失敗の理由です。</summary>
        public string error = string.Empty;
    }
}
#endif
