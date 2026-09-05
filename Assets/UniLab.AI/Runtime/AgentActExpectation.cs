#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>行動の事後条件を応答へ反映し、行動の受理結果とは分離します。</summary>
    internal static class AgentActExpectation
    {
        /// <summary>行動前後の観測で既存語彙を評価し、未達理由を一行ずつ返します。</summary>
        internal static void Apply(AiCommandResponse response, ScenarioExpectation[] expectations, UiSnapshotDocument before, UiSnapshotDocument after)
        {
            if (expectations == null || expectations.Length == 0)
            {
                response.expectOk = true;
                response.expectFailures = System.Array.Empty<string>();
                return;
            }

            var evaluator = new AgentExpectationEvaluator();
            response.expectOk = evaluator.Evaluate(expectations, after, UiSnapshot.Compare(before, after));
            response.expectFailures = new string[evaluator.Failures.Count];
            for (var failureIndex = 0; failureIndex < evaluator.Failures.Count; failureIndex++)
            {
                var failure = evaluator.Failures[failureIndex];
                response.expectFailures[failureIndex] = $" - {failure.kind} target={failure.target} value={failure.value} message={failure.message}"
                    .Replace("\r", "\\r").Replace("\n", "\\n");
            }
        }

        /// <summary>一括実行の打ち切り理由を、行動自体の成功を保持して返します。</summary>
        internal static bool ShouldStop(AiCommandResponse response)
        {
            if (response.expectOk)
            {
                return false;
            }

            response.message = "expect 未達で打ち切り";
            return true;
        }
    }
}
#endif
