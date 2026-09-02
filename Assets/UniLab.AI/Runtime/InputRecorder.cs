using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace UniLab.AI
{
    /// <summary>
    /// 再生可能な入力列を DebugOutput/replays 配下へ固定形式で残し、修正後の再現確認を機械処理できるようにする記録器です。
    /// </summary>
    public sealed class InputRecorder : IDisposable
    {
        private const string InputFileName = "inputs.jsonl";
        private const string ManifestFileName = "replay-manifest.json";

        private readonly List<InputRecordingEvent> _events = new List<InputRecordingEvent>();

#if ENABLE_INPUT_SYSTEM
        private Action<InputEventPtr, InputDevice> _onInputEvent;
#endif

        private string _outputDirectory;
        private InputReplayAnchor _currentAnchor;
        private int _startFrame;
        private int _currentAnchorFrame;
        private double _startRealtime;
        private bool _isRecording;

        /// <summary>
        /// 記録開始を明示 API にし、どの run を replay 資産化するかを呼び出し側が制御できるようにします。
        /// </summary>
        public void StartRecording(string outputDirectory)
        {
            if (_isRecording)
            {
                StopRecording();
            }

            _outputDirectory = outputDirectory ?? string.Empty;
            Directory.CreateDirectory(_outputDirectory);
            _events.Clear();
            _currentAnchor = null;
            _startFrame = Time.frameCount;
            _currentAnchorFrame = _startFrame;
            _startRealtime = Time.realtimeSinceStartupAsDouble;

#if ENABLE_INPUT_SYSTEM
            _onInputEvent = OnInputEvent;
            InputSystem.onEvent += _onInputEvent;
#endif
            _isRecording = true;
        }

        /// <summary>
        /// 次の入力列をどの待機条件に結び付けるかを明示し、ハイブリッド再生でロード揺らぎを吸収するための anchor 設定です。
        /// </summary>
        public void SetAnchor(InputReplayAnchor anchor)
        {
            _currentAnchor = anchor;
            _currentAnchorFrame = Time.frameCount;
        }

        /// <summary>
        /// 記録を確定して manifest を返し、後段が replay ディレクトリをそのまま参照できるようにします。
        /// </summary>
        public ReplayManifest StopRecording()
        {
            if (!_isRecording)
            {
                return BuildManifest();
            }

#if ENABLE_INPUT_SYSTEM
            if (_onInputEvent != null)
            {
                InputSystem.onEvent -= _onInputEvent;
                _onInputEvent = null;
            }
#endif

            _isRecording = false;
            WriteInputs();
            var manifest = BuildManifest();
            var manifestFilePath = Path.Combine(_outputDirectory, ManifestFileName);
            File.WriteAllText(manifestFilePath, JsonUtility.ToJson(manifest, true));
            return manifest;
        }

        /// <summary>
        /// 記録購読を必ず外し、次 run へイベント購読を漏らさないための破棄です。
        /// </summary>
        public void Dispose()
        {
            StopRecording();
        }

#if ENABLE_INPUT_SYSTEM
        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!_isRecording || device == null || !eventPtr.valid)
            {
                return;
            }

            if (eventPtr.IsA<TextEvent>())
            {
                RecordTextEvent(eventPtr, device);
                return;
            }

            if (device is Touchscreen && eventPtr.IsA<StateEvent>())
            {
                RecordTouchEvent(eventPtr, device);
                return;
            }

            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            {
                return;
            }

            foreach (var control in eventPtr.EnumerateChangedControls(device))
            {
                if (control == null || control.children.Count > 0)
                {
                    continue;
                }

                var value = control.ReadValueFromEventAsObject(eventPtr);
                if (value == null)
                {
                    continue;
                }

                _events.Add(new InputRecordingEvent
                {
                    frame = Time.frameCount - _startFrame,
                    time = (float)(Time.realtimeSinceStartupAsDouble - _startRealtime),
                    device = ResolveDeviceKind(device),
                    eventKind = "state",
                    control = BuildControlPath(control),
                    value = ConvertValueToString(value),
                    valueType = value.GetType().FullName,
                    relativeFrame = Time.frameCount - _currentAnchorFrame,
                    anchor = CloneAnchor(_currentAnchor),
                });
            }
        }

        private unsafe void RecordTextEvent(InputEventPtr eventPtr, InputDevice device)
        {
            var textEvent = TextEvent.From(eventPtr);
            _events.Add(new InputRecordingEvent
            {
                frame = Time.frameCount - _startFrame,
                time = (float)(Time.realtimeSinceStartupAsDouble - _startRealtime),
                device = ResolveDeviceKind(device),
                eventKind = "text",
                text = char.ConvertFromUtf32(textEvent->character),
                relativeFrame = Time.frameCount - _currentAnchorFrame,
                anchor = CloneAnchor(_currentAnchor),
            });
        }

        private void RecordTouchEvent(InputEventPtr eventPtr, InputDevice device)
        {
            try
            {
                var touchState = StateEvent.GetState<TouchState>(eventPtr);
                _events.Add(new InputRecordingEvent
                {
                    frame = Time.frameCount - _startFrame,
                    time = (float)(Time.realtimeSinceStartupAsDouble - _startRealtime),
                    device = ResolveDeviceKind(device),
                    eventKind = "touch",
                    touchId = touchState.touchId,
                    touchPhase = touchState.phase.ToString(),
                    x = touchState.position.x,
                    y = touchState.position.y,
                    deltaX = touchState.delta.x,
                    deltaY = touchState.delta.y,
                    relativeFrame = Time.frameCount - _currentAnchorFrame,
                    anchor = CloneAnchor(_currentAnchor),
                });
            }
            catch (Exception)
            {
            }
        }

        private static string ResolveDeviceKind(InputDevice device)
        {
            if (device is Gamepad)
            {
                return "Gamepad";
            }

            if (device is Keyboard)
            {
                return "Keyboard";
            }

            if (device is Mouse)
            {
                return "Mouse";
            }

            if (device is Touchscreen)
            {
                return "Touchscreen";
            }

            return device.layout;
        }

        private static string BuildControlPath(InputControl control)
        {
            var pathParts = new List<string>();
            var currentControl = control;
            while (currentControl != null && !(currentControl is InputDevice))
            {
                pathParts.Insert(0, currentControl.name);
                currentControl = currentControl.parent;
            }

            return string.Join("/", pathParts);
        }
#endif

        private void WriteInputs()
        {
            var inputFilePath = Path.Combine(_outputDirectory, InputFileName);
            using var writer = new StreamWriter(inputFilePath, false);
            for (var eventIndex = 0; eventIndex < _events.Count; eventIndex++)
            {
                writer.WriteLine(JsonUtility.ToJson(_events[eventIndex]));
            }
        }

        private ReplayManifest BuildManifest()
        {
            return new ReplayManifest
            {
                name = Path.GetFileName(_outputDirectory),
                recordingFramesPerSecond = Application.targetFrameRate,
                frameCount = Mathf.Max(0, Time.frameCount - _startFrame),
                inputCount = _events.Count,
                recordedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                saveBeforePath = string.Empty,
                seedValue = string.Empty,
                unityVersion = Application.unityVersion,
            };
        }

        private static InputReplayAnchor CloneAnchor(InputReplayAnchor source)
        {
            if (source == null)
            {
                return null;
            }

            return new InputReplayAnchor
            {
                waitForText = source.waitForText,
                waitForObject = source.waitForObject,
                waitForFocus = source.waitForFocus,
                waitForScene = source.waitForScene,
            };
        }

        private static string ConvertValueToString(object value)
        {
            switch (value)
            {
                case float floatValue:
                    return floatValue.ToString("R", CultureInfo.InvariantCulture);
                case double doubleValue:
                    return doubleValue.ToString("R", CultureInfo.InvariantCulture);
                case int intValue:
                    return intValue.ToString(CultureInfo.InvariantCulture);
                case bool boolValue:
                    return boolValue ? "true" : "false";
                case Vector2 vector2Value:
                    return $"{vector2Value.x.ToString("R", CultureInfo.InvariantCulture)},{vector2Value.y.ToString("R", CultureInfo.InvariantCulture)}";
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }
    }
}
#endif
