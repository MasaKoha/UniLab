#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// ステップ失敗時の証拠を固定パスで返し、ブリッジ側がファイルだけを見れば診断できるようにします。
    /// </summary>
    [Serializable]
    public sealed class ScenarioStepEvidence
    {
        /// <summary>
        /// 失敗時スクリーンショットのパスです。
        /// </summary>
        public string capture;

        /// <summary>
        /// 失敗時スナップショット JSON のパスです。
        /// </summary>
        public string snapshot;
    }
}
#endif
