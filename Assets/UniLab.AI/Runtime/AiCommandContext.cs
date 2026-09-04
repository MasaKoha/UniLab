#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>同期・非同期の双方で同じ引数検証と省略値を使います。</summary>
    internal sealed class AiCommandContext
    {
        private readonly Dictionary<string, string> _members;
        internal AiCommandArguments Arguments { get; }
        internal string Operation { get; }

        internal AiCommandContext(AiCommandRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Operation = request.op ?? string.Empty;
            var json = string.IsNullOrWhiteSpace(request.args) ? "{}" : request.args;
            _members = AiJsonObject.Parse(json);
            Arguments = new AiCommandArguments();
            JsonUtility.FromJsonOverwrite(json, Arguments);
            AiCommandArguments.ValidateDuration(Arguments.readyTimeoutSeconds, nameof(Arguments.readyTimeoutSeconds), false);
            if (Operation == "agent.observe")
            {
                UiObservationScope.Validate(Arguments.scope);
            }
        }

        internal string GetObject(string name, bool required = false)
        {
            if (!_members.TryGetValue(name, out var json))
            {
                if (required)
                {
                    throw new ArgumentException($"{name} が必要です。");
                }

                return string.Empty;
            }

            AiJsonObject.Parse(json);
            return json;
        }

        internal AgentAction[] GetActions()
        {
            var hasAction = _members.ContainsKey("action");
            var hasSteps = _members.TryGetValue("steps", out var stepsJson);
            if (hasAction == hasSteps)
            {
                throw new ArgumentException("action または steps のどちらか一方が必要です。");
            }

            if (hasAction)
            {
                return new[] { JsonUtility.FromJson<AgentAction>(GetObject("action", true)) };
            }

            var steps = AiJsonObject.ParseObjectArray(stepsJson);
            if (steps.Count == 0)
            {
                throw new ArgumentException("steps には 1 件以上の行動オブジェクトが必要です。");
            }

            var actions = new AgentAction[steps.Count];
            for (var actionIndex = 0; actionIndex < steps.Count; actionIndex++)
            {
                actions[actionIndex] = JsonUtility.FromJson<AgentAction>(steps[actionIndex]);
            }

            return actions;
        }
    }
}
#endif
