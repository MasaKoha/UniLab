#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>Play 状態に依存せず、空目標と自由行動モードを検証します。</summary>
    internal static class AgentGoalValidator
    {
        /// <summary>自由行動または期待値を持つ目標だけを受け付けます。</summary>
        internal static bool Validate(AgentGoal goal, out string message)
        {
            var valid = goal != null && (goal.freePlay || (goal.goal != null && goal.goal.Length > 0));
            message = valid ? string.Empty : AgentSessionCommands.EmptyGoalMessage;
            return valid;
        }
    }
}
#endif
