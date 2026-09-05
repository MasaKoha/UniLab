#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.AI
{
    /// <summary>CLI とメールボックスの操作実装を一箇所に集約します。</summary>
    public static class AiCommandDispatcher
    {
        private const string RunningStatusPrefix = "agent: status=running ";
        private static AiConsoleLog _console;

        /// <summary>同期で即時実行し、フレームをまたぐ操作は要求時点の結果を返します。</summary>
        public static AiCommandResponse Execute(AiCommandRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            AiCommandResponse response;
            try
            {
                response = ExecuteCore(new AiCommandContext(request));
            }
            catch (Exception exception)
            {
                response = Failure(request, exception.Message);
            }

            response.elapsedMs = (int)stopwatch.ElapsedMilliseconds;
            return response;
        }

        /// <summary>フレーム進行を許し、完了時に一度だけ結果を通知します。</summary>
        public static IEnumerator ExecuteAsync(AiCommandRequest request, Action<AiCommandResponse> onCompleted)
        {
            var stopwatch = Stopwatch.StartNew();
            AiCommandResponse response = null;
            using (var execution = ExecuteAsyncCore(request, result => response = result))
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = execution.MoveNext();
                    }
                    catch (Exception exception)
                    {
                        response = Failure(request, exception.Message);
                        break;
                    }

                    if (!hasNext)
                    {
                        break;
                    }

                    yield return execution.Current;
                }
            }

            response.elapsedMs = (int)stopwatch.ElapsedMilliseconds;
            onCompleted?.Invoke(response);
        }

        /// <summary>登録済み操作名の一覧を返します。</summary>
        public static string[] ListOps()
        {
            return new[] { "ping", "ops", "agent.begin", "agent.observe", "agent.act", "agent.goal", "agent.end", "agent.export", "capture", "snapshot", "console" };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetConsole()
        {
            _console?.Dispose();
            _console = new AiConsoleLog();
        }

        private static AiCommandResponse ExecuteCore(AiCommandContext context)
        {
            var operation = context.Operation;
            var arguments = context.Arguments;
            if (Array.IndexOf(ListOps(), operation) < 0)
            {
                return new AiCommandResponse { op = operation, error = "unknown op" };
            }

            if (operation.StartsWith("agent.", StringComparison.Ordinal) && !Application.isPlaying)
            {
                return new AiCommandResponse { op = operation, message = "playMode が必要です" };
            }

            switch (operation)
            {
                case "ping": return Success(operation, $"playMode={Application.isPlaying} scene={SceneManager.GetActiveScene().name} frame={Time.frameCount}");
                case "ops": return Success(operation, string.Join("\n", ListOps()));
                case "agent.begin": return ConvertResult(operation, AgentSessionCommands.Begin(context.GetObject("goal", true), context.GetObject("options")));
                case "agent.observe": return Observe(arguments);
                case "agent.act": return ActImmediately(context);
                case "agent.goal": return ConvertResult(operation, AgentSessionCommands.IsGoalReached());
                case "agent.end": return ConvertResult(operation, AgentSessionCommands.End());
                case "agent.export": return ConvertResult(operation, AgentSessionCommands.ExportAsScenario(arguments.name));
                case "capture": return Capture(arguments);
                case "snapshot": return Snapshot(arguments);
                case "console": return ReadConsole(arguments);
                default: throw new InvalidOperationException("登録済み操作の実装がありません。");
            }
        }

        private static System.Collections.Generic.IEnumerator<object> ExecuteAsyncCore(AiCommandRequest request, Action<AiCommandResponse> completed)
        {
            var context = new AiCommandContext(request);
            if (context.Operation == "agent.act" && Application.isPlaying)
            {
                using (var execution = ActAsync(context, completed))
                {
                    while (execution.MoveNext())
                    {
                        yield return execution.Current;
                    }
                }

                yield break;
            }

            var response = ExecuteCore(context);
            var hasCapture = context.Operation == "capture"
                || (context.Operation == "agent.observe" && !string.IsNullOrEmpty(context.Arguments.capture));
            if (hasCapture && response.ok)
            {
                using (var capture = AiCaptureSupport.CompleteAsync(response))
                {
                    while (capture.MoveNext())
                    {
                        yield return capture.Current;
                    }
                }
            }

            completed(response);
        }

        private static AiCommandResponse ActImmediately(AiCommandContext context)
        {
            AiCommandResponse response = null;
            foreach (var action in context.GetActions())
            {
                response = ConvertResult(context.Operation, AgentSessionCommands.Act(JsonUtility.ToJson(action)));
                if (!IsRunning(response))
                {
                    break;
                }
            }

            return response;
        }

        private static System.Collections.Generic.IEnumerator<object> ActAsync(AiCommandContext context, Action<AiCommandResponse> completed)
        {
            AiCommandResponse response = null;
            foreach (var action in context.GetActions())
            {
                using (var execution = ExecuteActionAsync(context, action, result => response = result))
                {
                    while (execution.MoveNext())
                    {
                        yield return execution.Current;
                    }
                }

                if (!IsRunning(response) || !response.settled)
                {
                    break;
                }
            }

            completed(response);
        }

        private static System.Collections.Generic.IEnumerator<object> ExecuteActionAsync(
            AiCommandContext context, AgentAction action, Action<AiCommandResponse> completed)
        {
            var targetSpecification = GetReadyTarget(action);
            var stopwatch = Stopwatch.StartNew();
            // 対象を持たない操作（press / move 等）は待つものが無いので準備済み扱いにする
            var ready = string.IsNullOrEmpty(targetSpecification);
            if (!string.IsNullOrEmpty(targetSpecification))
            {
                while (!(ready = UiReadiness.IsSubmittable(targetSpecification, out _))
                    && stopwatch.Elapsed.TotalSeconds < context.Arguments.readyTimeoutSeconds)
                {
                    yield return null;
                }
            }

            var waitedMilliseconds = string.IsNullOrEmpty(targetSpecification) ? 0 : (int)stopwatch.ElapsedMilliseconds;
            using (var settle = new AiSettleWait(context.Arguments))
            {
                // タイムアウトでも既存の入力経路へ渡し、拒否理由や座標入力の挙動を維持する。
                var response = ConvertResult(context.Operation, AgentSessionCommands.Act(JsonUtility.ToJson(action)));
                response.ready = ready;
                response.waitedMs = waitedMilliseconds;
                if (!response.ok || (!string.IsNullOrEmpty(targetSpecification) && !ready))
                {
                    completed(response);
                    yield break;
                }

                var wait = settle.Wait();
                while (wait.MoveNext())
                {
                    yield return wait.Current;
                }

                RefreshObservation(response);
                response.settled = settle.Settled;
                if (!response.settled)
                {
                    response.ok = false;
                    response.error = "settle timeout";
                }

                completed(response);
            }
        }

        private static string GetReadyTarget(AgentAction action)
        {
            if (!string.IsNullOrEmpty(action.submit))
            {
                return action.submit;
            }

            return !string.IsNullOrEmpty(action.click) ? action.click : action.tap;
        }

        private static void RefreshObservation(AiCommandResponse response)
        {
            var observation = ConvertResult(response.op, AgentSessionCommands.Observe(false));
            var firstLineEnd = response.text.IndexOf('\n');
            var statusLine = firstLineEnd < 0 ? response.text : response.text.Substring(0, firstLineEnd);
            response.text = statusLine + "\n" + observation.text;
            if (!observation.ok)
            {
                response.ok = false;
                response.message = observation.message;
            }
        }

        private static bool IsRunning(AiCommandResponse response)
        {
            return response.ok && response.text.StartsWith(RunningStatusPrefix, StringComparison.Ordinal);
        }

        private static AiCommandResponse ReadConsole(AiCommandArguments arguments)
        {
            AiConsoleLog.Validate(arguments.count, arguments.level);
            return Success("console", _console?.Read(arguments.count, arguments.level) ?? string.Empty);
        }

        private static AiCommandResponse Observe(AiCommandArguments arguments)
        {
            if (!string.IsNullOrEmpty(arguments.capture))
            {
                AiCaptureSupport.ValidateName(arguments.capture);
            }

            var response = ConvertResult("agent.observe", AgentSessionCommands.Observe(arguments.diffOnly, arguments.scope));
            if (response.ok && !string.IsNullOrEmpty(arguments.capture))
            {
                // 観測と撮影の間で yield せず、ターン進行によるフレームのずれを防ぐ。
                response.path = AiCaptureSupport.Request(arguments.capture, arguments.directory);
            }

            return response;
        }

        private static AiCommandResponse Capture(AiCommandArguments arguments)
        {
            return new AiCommandResponse
            {
                ok = true,
                op = "capture",
                path = AiCaptureSupport.Request(arguments.name, arguments.directory),
            };
        }

        private static AiCommandResponse Snapshot(AiCommandArguments arguments)
        {
            var snapshot = UiSnapshot.Capture();
            return new AiCommandResponse
            {
                ok = true,
                op = "snapshot",
                text = arguments.compact ? UiSnapshot.ToCompactText(snapshot, "all") : JsonUtility.ToJson(snapshot, true),
                path = arguments.save ? UiSnapshot.Save(snapshot) : string.Empty,
            };
        }

        private static AiCommandResponse ConvertResult(string operation, string json)
        {
            var result = JsonUtility.FromJson<AgentCommandResult>(json);
            return new AiCommandResponse
            {
                ok = result.ok, op = operation, session = result.session, message = result.message,
                text = result.text, path = result.path,
            };
        }

        private static AiCommandResponse Success(string operation, string text)
        {
            return new AiCommandResponse { ok = true, op = operation, text = text };
        }

        private static AiCommandResponse Failure(AiCommandRequest request, string error)
        {
            return new AiCommandResponse { op = request?.op ?? string.Empty, error = error };
        }
    }
}
#endif
