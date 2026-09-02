#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace UniLab.AI
{
    /// <summary>
    /// シナリオの重さを手作業の体感に頼らず比較できるよう、ステップ単位の指標へ揃えて保存する。
    /// </summary>
    public sealed class PerformanceRecorder : IDisposable
    {
        private const string DefaultScenarioName = "scenario";
        private const int Gen0Collection = 0;

        private readonly string _scenarioName;
        private readonly bool _recordingActive;
        private readonly List<PerformanceStepReport> _stepReports = new List<PerformanceStepReport>();
        private readonly List<float> _summaryFrameTimes = new List<float>();

        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _setPassRecorder;
        private PerformanceRecorderDriver _driver;
        private StepAccumulator _currentStep;
        private bool _isDisposed;
        private bool _isRunning;
        private int _summaryFrameCount;
        private long _summaryGcAllocBytes;
        private int _summaryGcCollections;
        private long _previousStepMemoryBytes = -1L;
        private long _memoryGrowthBytes;
        private int _memoryMonotonicGrowthSteps;
        private long _startedAtTicksUtc;

        /// <summary>
        /// 録画負荷が混ざった値かどうかを後段で区別できるよう、実行文脈を記録しておく。
        /// </summary>
        public PerformanceRecorder(string scenarioName = "", bool recordingActive = false)
        {
            _scenarioName = string.IsNullOrEmpty(scenarioName) ? DefaultScenarioName : scenarioName;
            _recordingActive = recordingActive;
        }

        /// <summary>
        /// 実行の先頭で明示開始させ、不要なフレームが混ざらない計測区間を作る。
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();

            if (_isRunning)
            {
                return;
            }

            _stepReports.Clear();
            _summaryFrameTimes.Clear();
            _summaryFrameCount = 0;
            _summaryGcAllocBytes = 0L;
            _summaryGcCollections = 0;
            _previousStepMemoryBytes = -1L;
            _memoryGrowthBytes = 0L;
            _memoryMonotonicGrowthSteps = 0;
            _currentStep = null;
            _startedAtTicksUtc = DateTime.UtcNow.Ticks;

            _gcAllocRecorder = TryStartRecorder(ProfilerCategory.Memory, "GC.Alloc");
            _drawCallsRecorder = TryStartRecorder(ProfilerCategory.Render, "Draw Calls Count");
            _setPassRecorder = TryStartRecorder(ProfilerCategory.Render, "SetPass Calls Count");

            _driver = PerformanceRecorderDriver.Create(this);
            _isRunning = true;
        }

        /// <summary>
        /// ステップ境界を明示させ、どの操作で負荷が跳ねたかをランナーが復元できるようにする。
        /// </summary>
        public void MarkStep(int stepIndex, string label)
        {
            ThrowIfDisposed();

            if (!_isRunning)
            {
                return;
            }

            FinalizeCurrentStep();
            _currentStep = new StepAccumulator(stepIndex, label);
        }

        /// <summary>
        /// 実行終了時点の集計を確定し、後段が JSON 保存やしきい値判定へ使える形へ固める。
        /// </summary>
        public PerformanceReport Stop()
        {
            ThrowIfDisposed();

            if (!_isRunning)
            {
                return CreateReport();
            }

            FinalizeCurrentStep();
            StopInternal();
            return CreateReport();
        }

        /// <summary>
        /// ステップ終了前の expect 判定で直近値を読むため、計測区間を閉じずに現在ステップだけを投影します。
        /// </summary>
        public PerformanceStepReport CaptureCurrentStepReport()
        {
            ThrowIfDisposed();

            if (_currentStep == null)
            {
                return null;
            }

            var totalMemoryBytes = Profiler.GetTotalAllocatedMemoryLong();
            var currentCollectionCount = GC.CollectionCount(Gen0Collection);
            var gcCollections = currentCollectionCount - _currentStep.Gen0CollectionCountAtStart;
            if (gcCollections < 0)
            {
                gcCollections = 0;
            }

            return new PerformanceStepReport(
                _currentStep.StepIndex,
                _currentStep.Label,
                _currentStep.FrameCount,
                CalculateAverage(_currentStep.FrameTimes),
                CalculatePercentile(_currentStep.FrameTimes, 95),
                CalculateMax(_currentStep.FrameTimes),
                _currentStep.GcAllocBytes,
                gcCollections,
                CalculateAverage(_currentStep.DrawCallsSum, _currentStep.DrawCallsCount),
                ConvertToIntOrFallback(_currentStep.DrawCallsMax, _currentStep.DrawCallsCount),
                CalculateAverage(_currentStep.SetPassCallsSum, _currentStep.SetPassCallsCount),
                ConvertToIntOrFallback(_currentStep.SetPassCallsMax, _currentStep.SetPassCallsCount),
                totalMemoryBytes);
        }

        /// <summary>
        /// 明示破棄を必須にし、隠し GameObject と ProfilerRecorder をセッション越しに残さない。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            StopInternal();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        internal void SampleFrame()
        {
            if (!_isRunning || _currentStep == null)
            {
                return;
            }

            var frameMilliseconds = Time.unscaledDeltaTime * 1000.0f;
            _currentStep.FrameTimes.Add(frameMilliseconds);
            _summaryFrameTimes.Add(frameMilliseconds);
            _currentStep.FrameCount++;
            _summaryFrameCount++;

            var gcAllocBytes = ReadRecorderValueOrDefault(_gcAllocRecorder, 0L);
            _currentStep.GcAllocBytes += gcAllocBytes;
            _summaryGcAllocBytes += gcAllocBytes;

            var drawCalls = ReadRecorderValueOrDefault(_drawCallsRecorder, -1L);
            if (drawCalls >= 0L)
            {
                _currentStep.DrawCallsSum += drawCalls;
                _currentStep.DrawCallsCount++;
                if (drawCalls > _currentStep.DrawCallsMax)
                {
                    _currentStep.DrawCallsMax = drawCalls;
                }
            }

            var setPassCalls = ReadRecorderValueOrDefault(_setPassRecorder, -1L);
            if (setPassCalls >= 0L)
            {
                _currentStep.SetPassCallsSum += setPassCalls;
                _currentStep.SetPassCallsCount++;
                if (setPassCalls > _currentStep.SetPassCallsMax)
                {
                    _currentStep.SetPassCallsMax = setPassCalls;
                }
            }
        }

        internal void HandleDriverDestroyed()
        {
            _driver = null;
        }

        private void FinalizeCurrentStep()
        {
            if (_currentStep == null)
            {
                return;
            }

            var totalMemoryBytes = Profiler.GetTotalAllocatedMemoryLong();
            var currentCollectionCount = GC.CollectionCount(Gen0Collection);
            var gcCollections = currentCollectionCount - _currentStep.Gen0CollectionCountAtStart;
            if (gcCollections < 0)
            {
                gcCollections = 0;
            }

            _summaryGcCollections += gcCollections;
            UpdateMemoryGrowth(totalMemoryBytes);

            _stepReports.Add(new PerformanceStepReport(
                _currentStep.StepIndex,
                _currentStep.Label,
                _currentStep.FrameCount,
                CalculateAverage(_currentStep.FrameTimes),
                CalculatePercentile(_currentStep.FrameTimes, 95),
                CalculateMax(_currentStep.FrameTimes),
                _currentStep.GcAllocBytes,
                gcCollections,
                CalculateAverage(_currentStep.DrawCallsSum, _currentStep.DrawCallsCount),
                ConvertToIntOrFallback(_currentStep.DrawCallsMax, _currentStep.DrawCallsCount),
                CalculateAverage(_currentStep.SetPassCallsSum, _currentStep.SetPassCallsCount),
                ConvertToIntOrFallback(_currentStep.SetPassCallsMax, _currentStep.SetPassCallsCount),
                totalMemoryBytes));

            _currentStep = null;
        }

        private void UpdateMemoryGrowth(long totalMemoryBytes)
        {
            if (_previousStepMemoryBytes < 0L)
            {
                _previousStepMemoryBytes = totalMemoryBytes;
                return;
            }

            var memoryDeltaBytes = totalMemoryBytes - _previousStepMemoryBytes;
            if (memoryDeltaBytes > 0L)
            {
                _memoryGrowthBytes += memoryDeltaBytes;
                _memoryMonotonicGrowthSteps++;
            }

            _previousStepMemoryBytes = totalMemoryBytes;
        }

        private PerformanceReport CreateReport()
        {
            var stepReports = _stepReports.ToArray();
            var summary = new PerformanceSummaryReport(
                _summaryFrameCount,
                CalculateAverage(_summaryFrameTimes),
                CalculatePercentile(_summaryFrameTimes, 95),
                CalculateMax(_summaryFrameTimes),
                _summaryGcAllocBytes,
                _summaryGcCollections,
                CalculateAverage(stepReports, true),
                CalculateMax(stepReports, true),
                CalculateAverage(stepReports, false),
                CalculateMax(stepReports, false),
                _memoryGrowthBytes,
                _memoryMonotonicGrowthSteps,
                _recordingActive);

            return new PerformanceReport(_scenarioName, new DateTime(_startedAtTicksUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture), stepReports, summary);
        }

        private void StopInternal()
        {
            if (_isRunning)
            {
                _isRunning = false;
            }

            StopRecorder(ref _gcAllocRecorder);
            StopRecorder(ref _drawCallsRecorder);
            StopRecorder(ref _setPassRecorder);

            if (_driver != null)
            {
                _driver.DestroySelf();
                _driver = null;
            }
        }

        private static ProfilerRecorder TryStartRecorder(ProfilerCategory category, string statisticName)
        {
            try
            {
                return ProfilerRecorder.StartNew(category, statisticName);
            }
            catch (ArgumentException)
            {
                return default;
            }
            catch (InvalidOperationException)
            {
                return default;
            }
        }

        private static void StopRecorder(ref ProfilerRecorder profilerRecorder)
        {
            if (!profilerRecorder.Valid)
            {
                profilerRecorder = default;
                return;
            }

            profilerRecorder.Dispose();
            profilerRecorder = default;
        }

        private static long ReadRecorderValueOrDefault(ProfilerRecorder profilerRecorder, long fallbackValue)
        {
            if (!profilerRecorder.Valid)
            {
                return fallbackValue;
            }

            if (profilerRecorder.Count <= 0)
            {
                return fallbackValue;
            }

            return profilerRecorder.LastValue;
        }

        private static float CalculateAverage(List<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0.0f;
            }

            var sum = 0.0f;
            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                sum += values[valueIndex];
            }

            return sum / values.Count;
        }

        private static float CalculateAverage(long sum, int count)
        {
            if (count <= 0)
            {
                return -1.0f;
            }

            return (float)sum / count;
        }

        private static float CalculateAverage(PerformanceStepReport[] stepReports, bool drawCalls)
        {
            if (stepReports == null || stepReports.Length == 0)
            {
                return -1.0f;
            }

            var sum = 0.0f;
            var count = 0;
            for (var reportIndex = 0; reportIndex < stepReports.Length; reportIndex++)
            {
                var value = drawCalls ? stepReports[reportIndex].drawCallsAvg : stepReports[reportIndex].setPassCallsAvg;
                if (value < 0.0f)
                {
                    continue;
                }

                sum += value;
                count++;
            }

            if (count == 0)
            {
                return -1.0f;
            }

            return sum / count;
        }

        private static int CalculateMax(PerformanceStepReport[] stepReports, bool drawCalls)
        {
            if (stepReports == null || stepReports.Length == 0)
            {
                return -1;
            }

            var maxValue = -1;
            for (var reportIndex = 0; reportIndex < stepReports.Length; reportIndex++)
            {
                var value = drawCalls ? stepReports[reportIndex].drawCallsMax : stepReports[reportIndex].setPassCallsMax;
                if (value > maxValue)
                {
                    maxValue = value;
                }
            }

            return maxValue;
        }

        private static float CalculatePercentile(List<float> values, int percentile)
        {
            if (values == null || values.Count == 0)
            {
                return 0.0f;
            }

            var sortedValues = new List<float>(values);
            sortedValues.Sort();

            var clampedPercentile = Mathf.Clamp(percentile, 0, 100);
            var lastIndex = sortedValues.Count - 1;
            var rawIndex = Mathf.CeilToInt((clampedPercentile / 100.0f) * lastIndex);
            var percentileIndex = Mathf.Clamp(rawIndex, 0, lastIndex);
            return sortedValues[percentileIndex];
        }

        private static float CalculateMax(List<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0.0f;
            }

            var maxValue = values[0];
            for (var valueIndex = 1; valueIndex < values.Count; valueIndex++)
            {
                if (values[valueIndex] > maxValue)
                {
                    maxValue = values[valueIndex];
                }
            }

            return maxValue;
        }

        private static int ConvertToIntOrFallback(long value, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            return (int)value;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PerformanceRecorder));
            }
        }

        private sealed class StepAccumulator
        {
            public readonly int StepIndex;
            public readonly string Label;
            public readonly List<float> FrameTimes = new List<float>();
            public readonly int Gen0CollectionCountAtStart;
            public int FrameCount;
            public long GcAllocBytes;
            public long DrawCallsSum;
            public int DrawCallsCount;
            public long DrawCallsMax = -1L;
            public long SetPassCallsSum;
            public int SetPassCallsCount;
            public long SetPassCallsMax = -1L;

            public StepAccumulator(int stepIndex, string label)
            {
                StepIndex = stepIndex;
                Label = string.IsNullOrEmpty(label) ? string.Empty : label;
                Gen0CollectionCountAtStart = GC.CollectionCount(Gen0Collection);
            }
        }
    }
}
#endif
