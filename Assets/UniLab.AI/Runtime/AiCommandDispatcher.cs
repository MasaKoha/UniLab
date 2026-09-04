#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.AI
{
    /// <summary>CLI とメールボックスの操作実装を一箇所に集約します。</summary>
    public static class AiCommandDispatcher
    {
        private const float CaptureTimeoutSeconds = 3f;
        private const string RunningStatusPrefix = "agent: status=running ";
        private static AiConsoleLog _console;

        static AiCommandDispatcher()
        {
            ResetConsole();
        }

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
                case "agent.observe": return ConvertResult(operation, AgentSessionCommands.Observe(arguments.diffOnly, arguments.scope));
                case "agent.act": return ActImmediately(context);
                case "agent.goal": return ConvertResult(operation, AgentSessionCommands.IsGoalReached());
                case "agent.end": return ConvertResult(operation, AgentSessionCommands.End());
                case "agent.export": return ConvertResult(operation, AgentSessionCommands.ExportAsScenario(arguments.name));
                case "capture": return Capture(arguments);
                case "snapshot": return Snapshot(arguments);
                case "console": return Success(operation, _console.Read(arguments.count));
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
            if (context.Operation == "capture" && response.ok)
            {
                var startedAt = Time.realtimeSinceStartup;
                do
                {
                    yield return null;
                }
                while (!File.Exists(response.path) && Time.realtimeSinceStartup - startedAt < CaptureTimeoutSeconds);
                response.settled = File.Exists(response.path);
                response.ok = response.settled;
                response.error = response.settled ? string.Empty : "capture timeout";
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

        private static AiCommandResponse Capture(AiCommandArguments arguments)
        {
            if (string.IsNullOrEmpty(arguments.name) || !Regex.IsMatch(arguments.name, @"\A[A-Za-z0-9_-]+\z"))
            {
                throw new ArgumentException("name は英数字・_・- のみで必ず指定してください。");
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var directory = string.IsNullOrEmpty(arguments.directory)
                ? Path.Combine(DebugOutputPath.DirectoryPath, "captures")
                : Path.GetFullPath(Path.Combine(projectRoot, arguments.directory));
            Directory.CreateDirectory(directory);
            var path = Path.GetFullPath(Path.Combine(directory, arguments.name + ".png"));
            // 前回のファイルを今回の撮影完了と誤認しないようにする。
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ScreenCapture.CaptureScreenshot(path);
            return new AiCommandResponse { ok = true, op = "capture", path = path };
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
