#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>入力取得と描画・履歴の寿命を結び付ける薄い入口です。</summary>
    public sealed class InputOverlayController : MonoBehaviour
    {
        private readonly InputOverlayLegacyInputSource _legacyInputSource = new InputOverlayLegacyInputSource();
        private readonly InputOverlayInputSystemSource _inputSystemSource = new InputOverlayInputSystemSource();
        private InputOverlayRenderer _renderer;

        /// <summary>描画・状態・履歴の寿命を明示初期化で揃えます。</summary>
        public void Initialize(InputOverlayOptions options)
        {
            if (_renderer == null)
            {
                _renderer = new InputOverlayRenderer(gameObject);
            }

            _renderer.Initialize(options);
        }

        /// <summary>実入力を伴わない操作も録画の履歴へ残します。</summary>
        public void AddSyntheticHistory(string label, float now)
        {
            _renderer.AddSyntheticHistory(label, now);
        }

        private void Update()
        {
            if (_renderer == null)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (!_inputSystemSource.TryPoll(this, now))
            {
                _legacyInputSource.Poll(this, now);
            }

            _renderer.Refresh(now);
        }

        private void OnDestroy()
        {
            _renderer?.Clear();
        }

        /// <summary>
        /// パッドボタン状態を更新します。
        /// 押下と解放の境界をここで持ち、保持表示と履歴追加を同じ判定で扱うためです。
        /// </summary>
        public void UpdateGamepadButtonState(string buttonKey, bool isPressed, float now)
        {
            _renderer.UpdateGamepadButtonState(buttonKey, isPressed, now);
        }

        /// <summary>
        /// パッドスティック位置を更新します。
        /// スティックは方向と倒し量が主体のため、ボタンと別の保持ロジックで扱います。
        /// </summary>
        public void SetGamepadSticks(Vector2 leftStick, Vector2 rightStick, float now)
        {
            _renderer.SetGamepadSticks(leftStick, rightStick, now);
        }

        /// <summary>
        /// ポインタ位置を更新します。
        /// キーボード＋マウス模式図の利用機器判定と画面上カーソル描画の両方で使うためです。
        /// </summary>
        public void SetPointerPosition(Vector2 screenPosition, float now)
        {
            _renderer.SetPointerPosition(screenPosition, now);
        }

        /// <summary>
        /// ポインタボタン状態を更新します。
        /// クリック波紋、押下保持、履歴追加、ドラッグ軌跡を同じ立ち上がり判定に束ねます。
        /// </summary>
        public void SetPointerButtons(bool isLeftPressed, bool isRightPressed, bool isMiddlePressed, float now)
        {
            _renderer.SetPointerButtons(isLeftPressed, isRightPressed, isMiddlePressed, now);
        }

        /// <summary>
        /// スクロール表示を短時間だけ残します。
        /// マウス移動と違って矢印が無いと録画上で操作を読み取りにくいためです。
        /// </summary>
        public void ShowScroll(Vector2 delta, float now)
        {
            _renderer.ShowScroll(delta, now);
        }

        /// <summary>
        /// アクティブなタッチ一覧を反映します。
        /// タップ開始だけ履歴へ残しつつ、描画そのものは接触中の指に限定します。
        /// </summary>
        public void ReplaceTouches(List<TouchSnapshot> touches, float now)
        {
            _renderer.ReplaceTouches(touches, now);
        }

        /// <summary>同時押しを含むキーボード状態を入力保持へ渡します。</summary>
        public void ReplacePressedKeyboardKeys(List<string> pressedKeys, float now)
        {
            _renderer.ReplacePressedKeyboardKeys(pressedKeys, now);
        }

        /// <summary>
        /// タッチの必要最小限データです。
        /// 実装差の大きい入力 API から描画側を切り離して簡素化します。
        /// </summary>
        public struct TouchSnapshot
        {
            /// <summary>
            /// 指を識別する ID です。
            /// マルチタッチを継続的に追跡するため保持します。
            /// </summary>
            public int touchId;

            /// <summary>
            /// 画面上の位置です。
            /// 録画上で実際にどこを触ったかを直接示します。
            /// </summary>
            public Vector2 position;
        }
    }
}
#endif
