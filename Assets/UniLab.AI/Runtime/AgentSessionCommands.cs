#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// MCP ブリッジの execute_code から文字列だけで呼べる静的入口です。
    /// インスタンス参照を外側へ保持させず、セッション操作を JSON に寄せるために用意します。
    /// </summary>
    public static class AgentSessionCommands
    {
        private static AgentSession _currentSession;

        /// <summary>
        /// 目標 JSON とオプション JSON から現在セッションを開始します。
        /// </summary>
        public static string Begin(string goalJson, string optionsJson)
        {
            _currentSession?.Dispose();
            var goal = string.IsNullOrEmpty(goalJson) ? new AgentGoal() : JsonUtility.FromJson<AgentGoal>(goalJson);
            var options = string.IsNullOrEmpty(optionsJson) ? new AgentOptions() : JsonUtility.FromJson<AgentOptions>(optionsJson);
            _currentSession = AgentSession.Begin(goal, options);
            return ToJson(true, _currentSession.SessionId, "セッションを開始しました。", _currentSession.Observe(false), _currentSession.OutputDirectory);
        }

        /// <summary>
        /// 現在セッションの観測を返し、外部 LLM の次手選択に使える形へ整えます。
        /// </summary>
        public static string Observe(bool diffOnly)
        {
            if (_currentSession == null)
            {
                return ToJson(false, string.Empty, "セッションが開始されていません。", string.Empty, string.Empty);
            }

            return ToJson(true, _currentSession.SessionId, "観測しました。", _currentSession.Observe(diffOnly), _currentSession.OutputDirectory);
        }

        /// <summary>
        /// 1 手 JSON を実行し、実行後の観測と拒否理由を同じ戻り値で返します。
        /// </summary>
        public static string Act(string actionJson)
        {
            if (_currentSession == null)
            {
                return ToJson(false, string.Empty, "セッションが開始されていません。", string.Empty, string.Empty);
            }

            var action = string.IsNullOrEmpty(actionJson) ? new AgentAction() : JsonUtility.FromJson<AgentAction>(actionJson);
            return ToJson(true, _currentSession.SessionId, "行動を処理しました。", _currentSession.Act(action), _currentSession.OutputDirectory);
        }

        /// <summary>
        /// 現在セッションの目標達成を評価し、成功自己申告なしで外側へ返します。
        /// </summary>
        public static string IsGoalReached()
        {
            if (_currentSession == null)
            {
                return ToJson(false, string.Empty, "セッションが開始されていません。", string.Empty, string.Empty);
            }

            var reached = _currentSession.IsGoalReached();
            return ToJson(true, _currentSession.SessionId, reached ? "目標を達成しています。" : "目標は未達です。", reached ? "true" : "false", _currentSession.OutputDirectory);
        }

        /// <summary>
        /// 成功した現在セッションを 02 のシナリオ JSON として書き出します。
        /// </summary>
        public static string ExportAsScenario(string name)
        {
            if (_currentSession == null)
            {
                return ToJson(false, string.Empty, "セッションが開始されていません。", string.Empty, string.Empty);
            }

            var path = _currentSession.ExportAsScenario(name);
            var ok = !string.IsNullOrEmpty(path);
            var message = ok ? "scenario.json を書き出しました。" : _currentSession.StatusMessage;
            return ToJson(ok, _currentSession.SessionId, message, string.Empty, path);
        }

        /// <summary>
        /// 現在セッションを終了し、入力状態とドライバを破棄します。
        /// </summary>
        public static string End()
        {
            if (_currentSession == null)
            {
                return ToJson(false, string.Empty, "セッションが開始されていません。", string.Empty, string.Empty);
            }

            var sessionId = _currentSession.SessionId;
            var outputDirectory = _currentSession.OutputDirectory;
            _currentSession.End();
            _currentSession.Dispose();
            _currentSession = null;
            return ToJson(true, sessionId, "セッションを終了しました。", string.Empty, outputDirectory);
        }

        private static string ToJson(bool ok, string session, string message, string text, string path)
        {
            var result = new AgentCommandResult
            {
                ok = ok,
                session = session ?? string.Empty,
                message = message ?? string.Empty,
                text = text ?? string.Empty,
                path = path ?? string.Empty,
            };
            return JsonUtility.ToJson(result, true);
        }
    }
}
#endif
