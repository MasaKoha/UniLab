#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 各ステップの合否と証拠を並べ、途中失敗後も全体の観測を残せるようにする JSON モデルです。
    /// </summary>
    [Serializable]
    public sealed class ScenarioStepResult
    {
        /// <summary>
        /// 元シナリオの並びと対応させるための 1 始まりの番号です。
        /// </summary>
        public int index;

        /// <summary>
        /// 旧 UI 操作の対象を結果から辿れるように残します。
        /// </summary>
        public string submit;

        /// <summary>
        /// 入力語彙の種類を結果から辿れるように残します。
        /// </summary>
        public string input;

        /// <summary>
        /// pass/fail をステップ単位で読めるようにします。
        /// </summary>
        public string status;

        /// <summary>
        /// 操作可能になるまでの実時間を性能診断へ流用できるようにします。
        /// </summary>
        public float waitedSeconds;

        /// <summary>
        /// 失敗した期待値を複数保持し、最初の失敗だけで情報を落とさないようにします。
        /// </summary>
        public ScenarioExpectationFailure[] failures;

        /// <summary>
        /// 失敗時の画像と構造 JSON を同じ階層で参照できるようにします。
        /// </summary>
        public ScenarioStepEvidence evidence;
    }
}
#endif
