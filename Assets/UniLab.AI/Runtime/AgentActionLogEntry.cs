#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 1 手ごとの観測と結果を JSONL に残し、失敗時も外側の判断を追跡できるようにする記録行です。
    /// </summary>
    [Serializable]
    public sealed class AgentActionLogEntry
    {
        /// <summary>1 始まりの手数です。</summary>
        public int step;

        /// <summary>操作前の画面を短く識別するための観測キーです。</summary>
        public string observationKey;

        /// <summary>実行した行動の種類です。</summary>
        public string actionKind;

        /// <summary>実行した行動の対象です。</summary>
        public string target;

        /// <summary>外部 LLM がその手を選んだ理由です。</summary>
        public string reason;

        /// <summary>拒否・成功・上限到達を外部から機械判定するための状態です。</summary>
        public string status;

        /// <summary>拒否や詰みを人間が読める形で残すための説明です。</summary>
        public string message;

        /// <summary>操作後差分を短く残し、無反応をあとから辿れるようにします。</summary>
        public string diff;

        /// <summary>記録時刻を動画やセッション JSON と突き合わせるための ISO 8601 文字列です。</summary>
        public string createdAt;
    }
}
#endif
