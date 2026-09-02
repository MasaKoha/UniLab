#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniLab.AI
{
    internal sealed class ForensicsPendingLog
    {
        public readonly string Condition;
        public readonly string StackTrace;
        public readonly LogType Type;
        public readonly string StackKey;

        public ForensicsPendingLog(string condition, string stackTrace, LogType type, string stackKey)
        {
            Condition = condition ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Type = type;
            StackKey = stackKey ?? string.Empty;
        }
    }
}
#endif
