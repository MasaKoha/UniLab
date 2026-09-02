#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// スナップショットから操作可能要素を選び、想定外の順序で例外や詰まりを掘る使い捨てランナーです。
    /// </summary>
    public sealed class MonkeyTester : MonoBehaviour
    {
        private const string MonkeyDirectoryName = "monkey";
        private const string TimestampFormat = "yyyyMMdd-HHmmss";
        private const string TraceFileName = "trace.jsonl";
        private const string CoverageFileName = "coverage.json";
        private const string ViolationsFileName = "violations.json";
        private const string SummaryFileName = "summary.json";
        private const string ReproFileName = "repro.json";
        private const int DefaultMaxSteps = 500;
        private const float DefaultMaxSeconds = 300.0f;
        private const float DefaultNoChangeTimeoutSeconds = 2.0f;
        private const int ReproStepCount = 20;
        private const int SameNoChangeLimit = 3;
        private const int AuditIntervalSteps = 25;

        private readonly List<MonkeyTraceEntry> _traceEntries = new List<MonkeyTraceEntry>();
        private readonly List<MonkeyViolation> _violations = new List<MonkeyViolation>();
        private readonly List<string> _recentTargets = new List<string>();
        private readonly Dictionary<string, int> _pressCountByPath = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _noChangeCountByPath = new Dictionary<string, int>();
        private readonly HashSet<string> _visitedScreens = new HashSet<string>();
        private readonly HashSet<string> _excludedPaths = new HashSet<string>();

        private MonkeyOptions _options;
        private System.Random _random;
        private string _outputDirectory;
        private ExceptionForensics _ownedForensics;
        private double _startedAt;
        private int _forensicsStartCount;
        private bool _completed;
        private bool _sessionStateEntered;

        /// <summary>
        /// 探索完了時に summary を返し、シナリオランナーが合否へ反映できるようにします。
        /// </summary>
        public event Action<MonkeySummary> Completed;

        /// <summary>
        /// 使い捨て GameObject を生成して探索を開始します。
        /// </summary>
        public static MonkeyTester Start(MonkeyOptions options)
        {
            var testerObject = new GameObject(nameof(MonkeyTester));
            DontDestroyOnLoad(testerObject);
            var tester = testerObject.AddComponent<MonkeyTester>();
            tester.Initialize(options);
            return tester;
        }

        private void Initialize(MonkeyOptions options)
        {
            _options = options ?? new MonkeyOptions();
            if (_options.maxSteps <= 0)
            {
                _options.maxSteps = DefaultMaxSteps;
            }

            if (_options.maxSeconds <= 0.0f)
            {
                _options.maxSeconds = DefaultMaxSeconds;
            }

            if (_options.noChangeTimeoutSeconds <= 0.0f)
            {
                _options.noChangeTimeoutSeconds = DefaultNoChangeTimeoutSeconds;
            }

            if (_options.excludePathContains == null || _options.excludePathContains.Length == 0)
            {
                _options.excludePathContains = new[] { "Delete", "Reset", "Quit" };
            }

            _random = new System.Random(_options.seed);
            _startedAt = Time.realtimeSinceStartupAsDouble;
            InitializeForensicsIfNeeded();
            _forensicsStartCount = ExceptionForensics.Current == null ? 0 : ExceptionForensics.Current.CapturedCount;
            _outputDirectory = Path.Combine(DebugOutputPath.DirectoryPath, MonkeyDirectoryName, DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            Directory.CreateDirectory(_outputDirectory);
            AiSessionState.Enter("monkey");
            _sessionStateEntered = true;
            StartCoroutine(RunCoroutine());
        }

        private void OnDestroy()
        {
            ExitSessionStateIfNeeded();
            _ownedForensics?.Dispose();
        }

        private IEnumerator RunCoroutine()
        {
            var stopReason = "maxSteps";
            for (var step = 1; step <= _options.maxSteps; step++)
            {
                if (Time.realtimeSinceStartupAsDouble - _startedAt > _options.maxSeconds)
                {
                    stopReason = "maxSeconds";
                    break;
                }

                var beforeSnapshot = UiSnapshot.Capture();
                _visitedScreens.Add(BuildScreenKey(beforeSnapshot));
                var candidates = CollectCandidates(beforeSnapshot);
                if (candidates.Count == 0)
                {
                    AddViolation(step, "stuck", string.Empty, "操作可能要素がありません。");
                    stopReason = "stuck";
                    break;
                }

                var target = ChooseCandidate(candidates);
                AddRecentTarget(target.path);
                yield return SubmitTargetCoroutine(target.path);
                var changeResult = new MonkeyChangeResult();
                yield return WaitForChangeOrTimeoutCoroutine(beforeSnapshot, changeResult);

                var afterSnapshot = UiSnapshot.Capture();
                var violation = false;
                var message = string.Empty;
                if (!changeResult.changed)
                {
                    violation = true;
                    message = "操作後にスナップショットが変化しませんでした。";
                    RegisterNoChange(target.path);
                    AddViolation(step, "noChange", target.path, message);
                }

                _traceEntries.Add(new MonkeyTraceEntry
                {
                    step = step,
                    frame = Time.frameCount,
                    target = target.path,
                    beforeScene = beforeSnapshot.activeScene,
                    afterScene = afterSnapshot.activeScene,
                    changed = changeResult.changed,
                    violation = violation,
                    waitedSeconds = changeResult.waitedSeconds,
                    message = message,
                });

                if (_options.stopOnViolation && _violations.Count > 0)
                {
                    stopReason = "violation";
                    break;
                }

                if (HasNewException())
                {
                    AddViolation(step, "exception", target.path, "例外またはエラーログを検出しました。");
                    stopReason = "exception";
                    break;
                }

                if (step % AuditIntervalSteps == 0 && !IsAuditClean())
                {
                    AddViolation(step, "audit", target.path, "レイアウト監査で検出があります。");
                    if (_options.stopOnViolation)
                    {
                        stopReason = "audit";
                        break;
                    }
                }
            }

            Complete(stopReason);
        }

        private IEnumerator SubmitTargetCoroutine(string targetPath)
        {
            if (_options.useRawInput && InputInjector.IsSupported && UiInputLocator.TryGetElementCenter(targetPath, out var screenPosition))
            {
                InputInjector.Click(screenPosition);
                yield return null;
                yield break;
            }

            var target = UiInputLocator.FindByPathSegment(targetPath);
            if (target != null)
            {
                UiInputLocator.TrySubmit(target);
            }

            yield return null;
        }

        private IEnumerator WaitForChangeOrTimeoutCoroutine(UiSnapshotDocument beforeSnapshot, MonkeyChangeResult result)
        {
            result.changed = false;
            result.waitedSeconds = 0.0f;
            var startedAt = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - startedAt < _options.noChangeTimeoutSeconds)
            {
                var afterSnapshot = UiSnapshot.Capture();
                var diff = UiSnapshot.Compare(beforeSnapshot, afterSnapshot);
                if (!diff.isEmpty)
                {
                    result.changed = true;
                    result.waitedSeconds = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
                    yield break;
                }

                yield return null;
            }

            result.waitedSeconds = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
        }

        private List<UiSnapshotElement> CollectCandidates(UiSnapshotDocument snapshot)
        {
            var candidates = new List<UiSnapshotElement>();
            if (snapshot == null || snapshot.elements == null)
            {
                return candidates;
            }

            for (var elementIndex = 0; elementIndex < snapshot.elements.Length; elementIndex++)
            {
                var element = snapshot.elements[elementIndex];
                if (element == null || !element.interactable || !string.IsNullOrEmpty(element.blockedBy))
                {
                    continue;
                }

                if (IsExcluded(element.path))
                {
                    continue;
                }

                candidates.Add(element);
            }

            return candidates;
        }

        private UiSnapshotElement ChooseCandidate(List<UiSnapshotElement> candidates)
        {
            var weighted = new List<UiSnapshotElement>();
            for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                var candidate = candidates[candidateIndex];
                var weight = _pressCountByPath.ContainsKey(candidate.path) ? 1 : 4;
                if (_recentTargets.Count > 0 && _recentTargets[_recentTargets.Count - 1] == candidate.path)
                {
                    weight = 1;
                }

                for (var weightIndex = 0; weightIndex < weight; weightIndex++)
                {
                    weighted.Add(candidate);
                }
            }

            var selected = weighted[_random.Next(weighted.Count)];
            _pressCountByPath.TryGetValue(selected.path, out var count);
            _pressCountByPath[selected.path] = count + 1;
            return selected;
        }

        private bool HasNewException()
        {
            return ExceptionForensics.Current != null && ExceptionForensics.Current.CapturedCount > _forensicsStartCount;
        }

        private static bool IsAuditClean()
        {
            var auditReport = UiLayoutAuditor.Audit();
            return auditReport.entries == null || auditReport.entries.Length == 0;
        }

        private void RegisterNoChange(string path)
        {
            _noChangeCountByPath.TryGetValue(path, out var count);
            count++;
            _noChangeCountByPath[path] = count;
            if (count >= SameNoChangeLimit)
            {
                _excludedPaths.Add(path);
            }
        }

        private void AddViolation(int step, string kind, string target, string message)
        {
            var directories = ExceptionForensics.Current == null ? Array.Empty<string>() : ExceptionForensics.Current.CapturedDirectories;
            var forensicsPath = directories.Length == 0 ? string.Empty : directories[directories.Length - 1];
            _violations.Add(new MonkeyViolation
            {
                step = step,
                kind = kind,
                target = target ?? string.Empty,
                message = message ?? string.Empty,
                forensicsPath = forensicsPath,
            });
        }

        private void Complete(string stopReason)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            ExitSessionStateIfNeeded();
            WriteOutputs(stopReason);
            var summary = BuildSummary(stopReason);
            Completed?.Invoke(summary);
            Destroy(gameObject);
        }

        private void InitializeForensicsIfNeeded()
        {
            if (ExceptionForensics.Current != null)
            {
                return;
            }

            _ownedForensics = new ExceptionForensics();
            _ownedForensics.Initialize();
        }

        private void WriteOutputs(string stopReason)
        {
            var traceFilePath = Path.Combine(_outputDirectory, TraceFileName);
            using (var writer = new StreamWriter(traceFilePath))
            {
                for (var traceIndex = 0; traceIndex < _traceEntries.Count; traceIndex++)
                {
                    writer.WriteLine(JsonUtility.ToJson(_traceEntries[traceIndex], false));
                }
            }

            var coverage = new MonkeyCoverage
            {
                visitedScreens = ToArray(_visitedScreens),
                pressedElements = ToArray(_pressCountByPath.Keys),
            };
            File.WriteAllText(Path.Combine(_outputDirectory, CoverageFileName), JsonUtility.ToJson(coverage, true));
            File.WriteAllText(Path.Combine(_outputDirectory, ViolationsFileName), JsonUtility.ToJson(new MonkeyViolationList { violations = _violations.ToArray() }, true));
            File.WriteAllText(Path.Combine(_outputDirectory, SummaryFileName), JsonUtility.ToJson(BuildSummary(stopReason), true));
            WriteReproScenario();
        }

        private void WriteReproScenario()
        {
            var start = Mathf.Max(0, _recentTargets.Count - ReproStepCount);
            var steps = new UiScenarioStep[_recentTargets.Count - start];
            for (var targetIndex = start; targetIndex < _recentTargets.Count; targetIndex++)
            {
                steps[targetIndex - start] = new UiScenarioStep { submit = _recentTargets[targetIndex], settleFrames = 1 };
            }

            var scenario = new UiScenario
            {
                name = "monkey-repro",
                steps = steps,
            };
            File.WriteAllText(Path.Combine(_outputDirectory, ReproFileName), UiScenarioJsonPresence.StripDefaultMonkey(JsonUtility.ToJson(scenario, true)));
        }

        private MonkeySummary BuildSummary(string stopReason)
        {
            return new MonkeySummary
            {
                seed = _options.seed,
                stepCount = _traceEntries.Count,
                durationSeconds = (float)(Time.realtimeSinceStartupAsDouble - _startedAt),
                violationCount = _violations.Count,
                pressedElementCount = _pressCountByPath.Count,
                visitedScreenCount = _visitedScreens.Count,
                stopReason = stopReason ?? string.Empty,
                outputDirectory = _outputDirectory,
            };
        }

        private void AddRecentTarget(string targetPath)
        {
            _recentTargets.Add(targetPath);
            if (_recentTargets.Count > ReproStepCount)
            {
                _recentTargets.RemoveAt(0);
            }
        }

        private bool IsExcluded(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            if (_excludedPaths.Contains(path))
            {
                return true;
            }

            for (var excludeIndex = 0; excludeIndex < _options.excludePathContains.Length; excludeIndex++)
            {
                var exclude = _options.excludePathContains[excludeIndex];
                if (!string.IsNullOrEmpty(exclude) && path.Contains(exclude))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildScreenKey(UiSnapshotDocument snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();
            builder.Append(snapshot.activeScene ?? string.Empty);
            if (snapshot.game != null)
            {
                for (var gameIndex = 0; gameIndex < snapshot.game.Length; gameIndex++)
                {
                    var entry = snapshot.game[gameIndex];
                    if (entry != null)
                    {
                        builder.Append("|").Append(entry.key).Append("=").Append(entry.value);
                    }
                }
            }

            return builder.ToString();
        }

        private static string[] ToArray(IEnumerable<string> values)
        {
            var list = new List<string>();
            foreach (var value in values)
            {
                list.Add(value);
            }

            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }

        private void ExitSessionStateIfNeeded()
        {
            if (!_sessionStateEntered)
            {
                return;
            }

            _sessionStateEntered = false;
            AiSessionState.Exit("monkey");
        }
    }
}
#endif
