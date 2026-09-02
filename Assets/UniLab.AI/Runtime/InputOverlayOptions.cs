#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 入力可視化オーバーレイの表示方針をまとめます。
    /// 録画用途を既定にしつつ、人向け出力では局所的に抑制できるようにします。
    /// </summary>
    public sealed class InputOverlayOptions
    {
        /// <summary>
        /// パッド入力を表示するかどうかです。
        /// 使わないデバイスを隠せるように既定値を公開します。
        /// </summary>
        public bool showGamepad = true;

        /// <summary>
        /// キーボード入力を表示するかどうかです。
        /// ショートカット主体の検証では押下の可視化が診断に直結するためです。
        /// </summary>
        public bool showKeyboard = true;

        /// <summary>
        /// ポインタとマウス操作を表示するかどうかです。
        /// OS カーソルが録画へ写らない環境でも操作位置を失わないようにします。
        /// </summary>
        public bool showPointer = true;

        /// <summary>
        /// タッチ入力を表示するかどうかです。
        /// 実機録画で指位置を追えない問題を避けるためです。
        /// </summary>
        public bool showTouch = true;

        /// <summary>
        /// 常時シルエットを表示するかどうかです。
        /// 何も押していない状態自体を録画へ残すため既定で有効にします。
        /// </summary>
        public bool alwaysShowSilhouette = true;

        /// <summary>
        /// 互換維持のため残す旧オプションです。
        /// 操作ラベルは履歴帯へ統合したため無視します。
        /// </summary>
        [Obsolete("showStepLabel は廃止されました。操作ラベルは履歴帯へ統合され、この設定は無視されます。")]
        public bool showStepLabel = true;

        /// <summary>
        /// ゲームパッド図を置く隅です。
        /// 右下を既定にしつつ、重要 UI を避けるために変更可能へします。
        /// </summary>
        public OverlayCorner gamepadCorner = OverlayCorner.BottomRight;

        /// <summary>
        /// 入力履歴帯を置く隅です。
        /// 既定は模式図の直上に相当する右下です。
        /// </summary>
        public OverlayCorner historyCorner = OverlayCorner.BottomRight;

        /// <summary>
        /// 互換維持のため残す旧オプションです。
        /// 操作ラベルは履歴帯へ統合したため無視します。
        /// </summary>
        [Obsolete("labelCorner は廃止されました。操作ラベルは履歴帯へ統合され、この設定は無視されます。")]
        public OverlayCorner labelCorner = OverlayCorner.TopRight;

        /// <summary>
        /// 全体スケールです。
        /// 解像度差や録画サイズ差で潰れないようにします。
        /// </summary>
        public float scale = 0.7f;

        /// <summary>
        /// 全体不透明度です。
        /// 画面内容を潰さずに入力だけ読める濃さへ合わせるためです。
        /// </summary>
        public float opacity = 0.85f;

        /// <summary>
        /// 離した後も押下ハイライトを保持する秒数です。
        /// 短い入力でも静止フレームで直前操作を判読できるようにします。
        /// </summary>
        public float holdSeconds = 0.6f;

        /// <summary>
        /// 履歴帯へ残す入力件数です。
        /// 直前の操作列を 1 フレームからでも読み返せる件数を既定値にします。
        /// </summary>
        public int historyCount = 6;

        /// <summary>
        /// 旧オプションです。
        /// 保持挙動は holdSeconds へ置き換えたため無視します。
        /// </summary>
        [Obsolete("minimumVisibleSeconds は廃止されました。保持時間は holdSeconds を使用し、この設定は無視されます。")]
        public float minimumVisibleSeconds = 0.3f;
    }
}
#endif
