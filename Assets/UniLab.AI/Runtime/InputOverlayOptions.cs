#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        /// シナリオ実行中のステップラベルを表示するかどうかです。
        /// 動画の時刻と実行中操作の対応を人間と AI の両方が追えるようにします。
        /// </summary>
        public bool showStepLabel = true;

        /// <summary>
        /// ゲームパッド図を置く隅です。
        /// 右下を既定にしつつ、重要 UI を避けるために変更可能へします。
        /// </summary>
        public OverlayCorner gamepadCorner = OverlayCorner.BottomRight;

        /// <summary>
        /// 全体スケールです。
        /// 解像度差や録画サイズ差で潰れないようにします。
        /// </summary>
        public float scale = 1f;

        /// <summary>
        /// 全体不透明度です。
        /// 画面内容を潰さずに入力だけ読める濃さへ合わせるためです。
        /// </summary>
        public float opacity = 0.85f;

        /// <summary>
        /// 短い押下を動画上で視認可能にする最低表示秒数です。
        /// 1 フレーム押下が 1 コマで消えると診断価値を失うためです。
        /// </summary>
        public float minimumVisibleSeconds = 0.3f;
    }
}
#endif
