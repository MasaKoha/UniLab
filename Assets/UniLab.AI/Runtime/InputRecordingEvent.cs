using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// フレーム同期で入力を戻せる最小単位として、1 変更を 1 行 JSON で残すための記録です。
    /// </summary>
    [Serializable]
    public sealed class InputRecordingEvent
    {
        /// <summary>
        /// 録画開始からの相対フレーム。固定ステップ再生の基準にするため保持します。
        /// </summary>
        public int frame;

        /// <summary>
        /// 録画開始からの相対秒。人間がログを読むときの時刻参照に使います。
        /// </summary>
        public float time;

        /// <summary>
        /// 再生先の仮想デバイス種別を決めるための論理デバイス名です。
        /// </summary>
        public string device;

        /// <summary>
        /// 再生方法の分岐を固定するための種別です。state / text / touch を使います。
        /// </summary>
        public string eventKind;

        /// <summary>
        /// state 再生時に同じ control へ値を書き戻すための相対パスです。
        /// </summary>
        public string control;

        /// <summary>
        /// JsonUtility で単純に保つため、値は文字列化して保存します。
        /// </summary>
        public string value;

        /// <summary>
        /// 文字列値の復元方法を固定するための型名です。
        /// </summary>
        public string valueType;

        /// <summary>
        /// テキスト入力を state 変更と区別して戻すための 1 文字です。
        /// </summary>
        public string text;

        /// <summary>
        /// Touchscreen では control 単位より生の TouchState の方が再現性が高いため保持します。
        /// </summary>
        public int touchId;

        /// <summary>
        /// TouchPhase を文字列で固定し、列挙値の将来差異を避けます。
        /// </summary>
        public string touchPhase;

        /// <summary>
        /// タッチ座標をそのまま戻し、ピンチやスワイプの軌跡を再現するための値です。
        /// </summary>
        public float x;

        /// <summary>
        /// タッチ座標をそのまま戻し、ピンチやスワイプの軌跡を再現するための値です。
        /// </summary>
        public float y;

        /// <summary>
        /// Touchscreen の内部補間差異を減らすため、元イベントの delta も残します。
        /// </summary>
        public float deltaX;

        /// <summary>
        /// Touchscreen の内部補間差異を減らすため、元イベントの delta も残します。
        /// </summary>
        public float deltaY;

        /// <summary>
        /// 条件充足からの相対フレームで打てるようにし、ロード時間のゆらぎを吸収します。
        /// </summary>
        public int relativeFrame;

        /// <summary>
        /// ハイブリッド再生の同期点です。空なら純粋なフレーム再生として扱います。
        /// </summary>
        public InputReplayAnchor anchor;
    }
}
#endif
