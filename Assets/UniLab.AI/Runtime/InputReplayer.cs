using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
using Unity.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace UniLab.AI
{
    /// <summary>
    /// 入力記録を固定ステップで戻し、修正前後で同じ入力列を何度でも当てられるようにする再生器です。
    /// </summary>
    public sealed class InputReplayer : MonoBehaviour
    {
        private const string InputFileName = "inputs.jsonl";
        private const string ManifestFileName = "replay-manifest.json";
        private const double WaitTimeoutSeconds = 30.0;

        private readonly List<InputRecordingEvent> _events = new List<InputRecordingEvent>();

#if ENABLE_INPUT_SYSTEM
        private readonly Dictionary<string, InputDevice> _devices = new Dictionary<string, InputDevice>();
#endif

        private ReplayManifest _manifest;
        private string _replayDirectory;
        private int _replayStartFrame;
        private int _nextEventIndex;
        private int _playedInputCount;
        private int _previousCaptureFramerate;
        private int _currentAnchorSatisfiedFrame;
        private double _anchorWaitStartedAt;
        private bool _isWaitingForAnchor;
        private bool _hasMismatch;
        private bool _completed;

        /// <summary>
        /// 完了イベントで件数と不一致を返し、外側のシナリオや expect 評価へ結果を渡せるようにします。
        /// </summary>
        public event Action<ReplayResult> Completed;

        /// <summary>
        /// replay ディレクトリから再生を開始し、フレーム更新と寿命管理をコンポーネントへ閉じ込めるための入口です。
        /// </summary>
        public static InputReplayer StartReplay(string replayDirectory)
        {
            var replayerObject = new GameObject(nameof(InputReplayer));
            DontDestroyOnLoad(replayerObject);
            var replayer = replayerObject.AddComponent<InputReplayer>();
            replayer.Initialize(replayDirectory);
            return replayer;
        }

        private void Update()
        {
            DriveReplay();
        }

        private void OnDestroy()
        {
            RestoreCaptureFramerate();
#if ENABLE_INPUT_SYSTEM
            foreach (var device in _devices.Values)
            {
                InputSystem.RemoveDevice(device);
            }

            _devices.Clear();
#endif
        }

        private void Initialize(string replayDirectory)
        {
            _replayDirectory = replayDirectory ?? string.Empty;
            var manifestFilePath = Path.Combine(_replayDirectory, ManifestFileName);
            if (File.Exists(manifestFilePath))
            {
                _manifest = JsonUtility.FromJson<ReplayManifest>(File.ReadAllText(manifestFilePath));
            }

            var inputFilePath = Path.Combine(_replayDirectory, InputFileName);
            if (File.Exists(inputFilePath))
            {
                var lines = File.ReadAllLines(inputFilePath);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                    {
                        continue;
                    }

                    var recordingEvent = JsonUtility.FromJson<InputRecordingEvent>(lines[lineIndex]);
                    if (recordingEvent != null)
                    {
                        _events.Add(recordingEvent);
                    }
                }
            }

            _replayStartFrame = Time.frameCount;
            _previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = _manifest != null && _manifest.recordingFramesPerSecond > 0 ? _manifest.recordingFramesPerSecond : 60;
        }

        private void DriveReplay()
        {
            if (_completed)
            {
                return;
            }

            if (_nextEventIndex >= _events.Count)
            {
                CompleteReplay(string.Empty);
                return;
            }

            var recordingEvent = _events[_nextEventIndex];
            if (HasAnchor(recordingEvent))
            {
                if (!_isWaitingForAnchor)
                {
                    _isWaitingForAnchor = true;
                    _anchorWaitStartedAt = Time.realtimeSinceStartupAsDouble;
                }

                if (!UiInputLocator.IsAnchorSatisfied(recordingEvent.anchor))
                {
                    if (Time.realtimeSinceStartupAsDouble - _anchorWaitStartedAt > WaitTimeoutSeconds)
                    {
                        _hasMismatch = true;
                        CompleteReplay("anchor 条件が満たされませんでした。");
                    }

                    return;
                }

                if (_currentAnchorSatisfiedFrame == 0)
                {
                    _currentAnchorSatisfiedFrame = Time.frameCount;
                }

                if (Time.frameCount - _currentAnchorSatisfiedFrame < recordingEvent.relativeFrame)
                {
                    return;
                }
            }
            else
            {
                if (Time.frameCount - _replayStartFrame < recordingEvent.frame)
                {
                    return;
                }
            }

            if (!TryPlayEvent(recordingEvent))
            {
                _hasMismatch = true;
                CompleteReplay($"入力の再生に失敗しました。 device={recordingEvent.device} control={recordingEvent.control}");
                return;
            }

            _playedInputCount++;
            _nextEventIndex++;
            if (_nextEventIndex < _events.Count && !AnchorsEqual(recordingEvent.anchor, _events[_nextEventIndex].anchor))
            {
                _isWaitingForAnchor = false;
                _currentAnchorSatisfiedFrame = 0;
            }
        }

        private bool TryPlayEvent(InputRecordingEvent recordingEvent)
        {
#if ENABLE_INPUT_SYSTEM
            var device = GetOrCreateDevice(recordingEvent.device);
            if (device == null)
            {
                return false;
            }

            if (recordingEvent.eventKind == "text")
            {
                if (string.IsNullOrEmpty(recordingEvent.text))
                {
                    return false;
                }

                InputSystem.QueueTextEvent(device, recordingEvent.text[0]);
                InputSystem.Update();
                return true;
            }

            if (recordingEvent.eventKind == "touch")
            {
                if (!(device is Touchscreen touchscreen))
                {
                    return false;
                }

                if (!Enum.TryParse(recordingEvent.touchPhase, out TouchPhase touchPhase))
                {
                    touchPhase = TouchPhase.Moved;
                }

                var touchState = new TouchState
                {
                    touchId = recordingEvent.touchId,
                    phase = touchPhase,
                    position = new Vector2(recordingEvent.x, recordingEvent.y),
                    delta = new Vector2(recordingEvent.deltaX, recordingEvent.deltaY),
                    pressure = touchPhase == TouchPhase.Ended ? 0.0f : 1.0f,
                };
                InputSystem.QueueStateEvent(touchscreen, touchState);
                InputSystem.Update();
                return true;
            }

            var control = device.TryGetChildControl(recordingEvent.control);
            if (control == null)
            {
                return false;
            }

            using var eventBuffer = StateEvent.From(device, out var eventPtr, Allocator.Temp);
            control.WriteValueFromObjectIntoEvent(eventPtr, ParseValue(recordingEvent.valueType, recordingEvent.value));
            InputSystem.QueueEvent(eventPtr);
            InputSystem.Update();
            return true;
#else
            return false;
#endif
        }

        private void CompleteReplay(string message)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            RestoreCaptureFramerate();
            var result = new ReplayResult(_playedInputCount, _hasMismatch, message);
            Completed?.Invoke(result);
            Destroy(gameObject);
        }

        private void RestoreCaptureFramerate()
        {
            Time.captureFramerate = _previousCaptureFramerate;
        }

        private static bool HasAnchor(InputRecordingEvent recordingEvent)
        {
            return recordingEvent != null
                && recordingEvent.anchor != null
                && (!string.IsNullOrEmpty(recordingEvent.anchor.waitForObject)
                    || !string.IsNullOrEmpty(recordingEvent.anchor.waitForText)
                    || !string.IsNullOrEmpty(recordingEvent.anchor.waitForFocus)
                    || !string.IsNullOrEmpty(recordingEvent.anchor.waitForScene));
        }

        private static bool AnchorsEqual(InputReplayAnchor first, InputReplayAnchor second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            if (first == null || second == null)
            {
                return false;
            }

            return first.waitForObject == second.waitForObject
                && first.waitForText == second.waitForText
                && first.waitForFocus == second.waitForFocus
                && first.waitForScene == second.waitForScene;
        }

#if ENABLE_INPUT_SYSTEM
        private InputDevice GetOrCreateDevice(string deviceKind)
        {
            var resolvedDeviceKind = string.IsNullOrEmpty(deviceKind) ? "Keyboard" : deviceKind;
            if (_devices.TryGetValue(resolvedDeviceKind, out var existingDevice))
            {
                return existingDevice;
            }

            InputDevice createdDevice;
            switch (resolvedDeviceKind)
            {
                case "Gamepad":
                    createdDevice = InputSystem.AddDevice<Gamepad>("UniLabAI Replay Gamepad");
                    break;
                case "Keyboard":
                    createdDevice = InputSystem.AddDevice<Keyboard>("UniLabAI Replay Keyboard");
                    break;
                case "Mouse":
                    createdDevice = InputSystem.AddDevice<Mouse>("UniLabAI Replay Mouse");
                    break;
                case "Touchscreen":
                    createdDevice = InputSystem.AddDevice<Touchscreen>("UniLabAI Replay Touchscreen");
                    break;
                default:
                    return null;
            }

            _devices.Add(resolvedDeviceKind, createdDevice);
            return createdDevice;
        }

        private static object ParseValue(string valueType, string value)
        {
            if (valueType == typeof(float).FullName)
            {
                return float.Parse(value, CultureInfo.InvariantCulture);
            }

            if (valueType == typeof(int).FullName)
            {
                return int.Parse(value, CultureInfo.InvariantCulture);
            }

            if (valueType == typeof(uint).FullName)
            {
                return uint.Parse(value, CultureInfo.InvariantCulture);
            }

            if (valueType == typeof(short).FullName)
            {
                return short.Parse(value, CultureInfo.InvariantCulture);
            }

            if (valueType == typeof(ushort).FullName)
            {
                return ushort.Parse(value, CultureInfo.InvariantCulture);
            }

            if (valueType == typeof(byte).FullName)
            {
                return byte.Parse(value, CultureInfo.InvariantCulture);
            }

            if (valueType == typeof(double).FullName)
            {
                return double.Parse(value, CultureInfo.InvariantCulture);
            }

            if (valueType == typeof(bool).FullName)
            {
                return bool.Parse(value);
            }

            if (valueType == typeof(Vector2).FullName)
            {
                var parts = value.Split(',');
                return new Vector2(
                    float.Parse(parts[0], CultureInfo.InvariantCulture),
                    float.Parse(parts[1], CultureInfo.InvariantCulture));
            }

            return float.Parse(value, CultureInfo.InvariantCulture);
        }
#endif
    }
}
#endif
