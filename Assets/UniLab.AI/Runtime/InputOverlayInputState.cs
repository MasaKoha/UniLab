#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>入力の押下集合と押下・解放の状態遷移を保持します。</summary>
    internal sealed class InputOverlayInputState
    {
        private const float StickCenterThreshold = 0.0001f;
        private readonly Action<string, float> _addHistoryEntry;
        private readonly Action<float> _registerKeyboardActivity;
        private InputOverlayOptions _options;

        /// <summary>通知先を初期化時に固定し、毎フレームのデリゲート生成を避けます。</summary>
        internal InputOverlayInputState(Action<string, float> addHistoryEntry, Action<float> registerKeyboardActivity)
        {
            _addHistoryEntry = addHistoryEntry;
            _registerKeyboardActivity = registerKeyboardActivity;
        }

        /// <summary>描画時の列挙でもボックス化を増やさない具体型の参照です。</summary>
        internal Dictionary<string, InputOverlayHeldState> KeyboardStatesByKey => _keyboardStatesByKey;

        /// <summary>保持時間の変更を既存状態にも反映します。</summary>
        internal void ApplyOptions(InputOverlayOptions options)
        {
            _options = options;
        }

        private readonly Dictionary<string, InputOverlayHeldState> _keyboardStatesByKey = new Dictionary<string, InputOverlayHeldState>(StringComparer.Ordinal);

        /// <summary>
        /// キー押下集合を更新します。
        /// キーごとの立ち上がりを保持して、再押下回数と履歴追加を polling でも再現するためです。
        /// </summary>
        public void ReplacePressedKeyboardKeys(List<string> pressedKeys, float now)
        {
            if (pressedKeys.Count > 0)
            {
                _registerKeyboardActivity(now);
            }

            for (var keyIndex = 0; keyIndex < pressedKeys.Count; keyIndex++)
            {
                var keyName = pressedKeys[keyIndex];
                if (string.IsNullOrEmpty(keyName))
                {
                    continue;
                }

                if (!_keyboardStatesByKey.TryGetValue(keyName, out var state))
                {
                    state = new InputOverlayHeldState(keyName);
                    _keyboardStatesByKey.Add(keyName, state);
                }

                UpdateHeldState(state, true, now, keyName, true);
            }

            foreach (var pair in _keyboardStatesByKey)
            {
                if (pressedKeys.Contains(pair.Key))
                {
                    continue;
                }

                UpdateHeldState(pair.Value, false, now, string.Empty, false);
            }
        }

        /// <summary>押下の境界だけで反復回数と履歴を更新します。</summary>
        internal void UpdateHeldState(InputOverlayHeldState state, bool isPressed, float now, string historyLabel, bool addHistoryOnPressed)
        {
            if (state == null)
            {
                return;
            }

            if (isPressed)
            {
                var isNewPress = !state.isPressed;
                if (isNewPress)
                {
                    state.repeatCount = state.IsVisible(now, _options.holdSeconds) ? state.repeatCount + 1 : 1;
                    state.lastPressedAt = now;
                    state.lastReleasedAt = -1f;
                    state.lastVisibleAt = now;
                    if (addHistoryOnPressed && !string.IsNullOrEmpty(historyLabel))
                    {
                        _addHistoryEntry(historyLabel, now);
                    }
                }
                else
                {
                    state.lastVisibleAt = now;
                }

                state.isPressed = true;
                return;
            }

            if (!state.isPressed)
            {
                return;
            }

            state.isPressed = false;
            state.lastReleasedAt = now;
            state.lastVisibleAt = now;
        }

        /// <summary>スティックの残像を従来の時刻規則で保持します。</summary>
        internal void UpdateStickState(ref Vector2 liveValue, ref Vector2 ghostValue, ref float ghostReleasedAt, Vector2 newValue, float now)
        {
            var clampedValue = Vector2.ClampMagnitude(newValue, 1f);
            liveValue = clampedValue;
            if (clampedValue.sqrMagnitude > StickCenterThreshold)
            {
                ghostValue = clampedValue;
                ghostReleasedAt = now;
                return;
            }

            if (ghostValue.sqrMagnitude > StickCenterThreshold)
            {
                ghostReleasedAt = now;
            }
        }
    }
}
#endif
