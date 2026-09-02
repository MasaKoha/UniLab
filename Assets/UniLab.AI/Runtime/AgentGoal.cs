#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 02 の expect 語彙をそのまま目標へ使い、新しい判定言語を増やさないための入力 JSON です。
    /// </summary>
    [Serializable]
    public sealed class AgentGoal
    {
        /// <summary>
        /// 達成判定を UiScenarioRunner と同じ語彙で共有するための期待値配列です。
        /// </summary>
        public ScenarioExpectation[] goal;

        /// <summary>
        /// LLM が無限に手を選び続けることを防ぐための手数上限です。
        /// </summary>
        public int maxSteps;

        /// <summary>
        /// 応答待ちや非同期入力を含めた実時間の安全弁です。
        /// </summary>
        public int maxSeconds;

        /// <summary>
        /// 削除やリセットのような危険操作を要素名の部分一致で拒否するための語彙です。
        /// </summary>
        public string[] forbid;
    }
}
#endif
