#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// セッション単位の入力方式と安全弁を外部運転手が固定できるようにするための設定です。
    /// </summary>
    [Serializable]
    public sealed class AgentOptions
    {
        /// <summary>
        /// 生入力の候補を必要な方式だけに絞り、LLM に自由記述させないための入力方式です。
        /// </summary>
        public string inputMode;

        /// <summary>
        /// 同じ観測と同じ行動の反復を詰みとして扱うしきい値です。
        /// </summary>
        public int stuckRepeatLimit;

        /// <summary>
        /// 操作後に外側が待つべきフレーム数を返し、同期ブリッジでも観測タイミングを揃えやすくします。
        /// </summary>
        public int settleFrames;

        /// <summary>
        /// 外部理由が空の手でも actions.jsonl を一定形式で残すための既定理由です。
        /// </summary>
        public string defaultReason;
    }
}
#endif
