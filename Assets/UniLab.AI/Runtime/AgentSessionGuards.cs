#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>予算・禁止語・反復履歴による停止判定をまとめます。</summary>
    internal sealed class AgentSessionGuards
    {
        private const int DefaultMaxSteps = 200;
        private const int DefaultMaxSeconds = 600;
        private const int DefaultStuckRepeatLimit = 3;

        private readonly AgentGoal _goal;
        private readonly AgentOptions _options;
        private string _lastObservationKey;
        private string _lastRepeatedObservationKey;
        private string _lastActionKey;
        private int _sameObservationActionCount;
        private int _sameObservationCount;

        /// <summary>セッション固有の予算と反復履歴を分離するために設定を受け取ります。</summary>
        internal AgentSessionGuards(AgentGoal goal, AgentOptions options)
        {
            _goal = goal;
            _options = options;
        }

        /// <summary>同じ観測または観測と行動の反復による停止を判定します。</summary>
        internal bool IsStuck(string observationKey, string actionKey)
        {
            if (string.Equals(_lastRepeatedObservationKey, observationKey, StringComparison.Ordinal))
            {
                _sameObservationCount++;
            }
            else
            {
                _sameObservationCount = 1;
            }

            _lastRepeatedObservationKey = observationKey;
            if (string.Equals(_lastObservationKey, observationKey, StringComparison.Ordinal) && string.Equals(_lastActionKey, actionKey, StringComparison.Ordinal))
            {
                _sameObservationActionCount++;
            }
            else
            {
                _sameObservationActionCount = 1;
            }

            _lastObservationKey = observationKey;
            _lastActionKey = actionKey;
            return _sameObservationCount >= ResolveStuckRepeatLimit() || _sameObservationActionCount >= ResolveStuckRepeatLimit();
        }

        /// <summary>禁止語への部分一致を大文字小文字を区別せず判定します。</summary>
        internal bool IsForbidden(string actionKey, string target)
        {
            var forbiddenWords = _goal.forbid;
            if (forbiddenWords == null || forbiddenWords.Length == 0)
            {
                return false;
            }

            for (var forbiddenIndex = 0; forbiddenIndex < forbiddenWords.Length; forbiddenIndex++)
            {
                var forbiddenWord = forbiddenWords[forbiddenIndex];
                if (string.IsNullOrEmpty(forbiddenWord))
                {
                    continue;
                }

                if (ContainsOrdinalIgnoreCase(actionKey, forbiddenWord) || ContainsOrdinalIgnoreCase(target, forbiddenWord))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>実行済みの手数が予算に到達したかを判定します。</summary>
        internal bool IsStepBudgetExceeded(int stepCount)
        {
            return stepCount >= ResolveMaxSteps();
        }

        /// <summary>開始からの実時間が予算に到達したかを判定します。</summary>
        internal bool IsTimeBudgetExceeded(double startedAtRealtime, double currentRealtime)
        {
            return currentRealtime - startedAtRealtime >= ResolveMaxSeconds();
        }

        /// <summary>未指定時も手数予算を既定値に固定します。</summary>
        internal int ResolveMaxSteps()
        {
            return _goal.maxSteps > 0 ? _goal.maxSteps : DefaultMaxSteps;
        }

        /// <summary>未指定時も実時間予算を既定値に固定します。</summary>
        internal int ResolveMaxSeconds()
        {
            return _goal.maxSeconds > 0 ? _goal.maxSeconds : DefaultMaxSeconds;
        }

        private int ResolveStuckRepeatLimit()
        {
            return _options.stuckRepeatLimit > 0 ? _options.stuckRepeatLimit : DefaultStuckRepeatLimit;
        }

        private static bool ContainsOrdinalIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
