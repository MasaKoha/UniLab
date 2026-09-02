#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 例外発生時にランナーの現在地を静的に渡し、DI なしの汎用ライブラリでも証拠を紐付けられるようにします。
    /// </summary>
    public static class ForensicsContext
    {
        /// <summary>
        /// 実行中シナリオの名前です。
        /// </summary>
        public static string ScenarioName { get; private set; }

        /// <summary>
        /// 実行中ステップ番号です。
        /// </summary>
        public static int StepIndex { get; private set; }

        /// <summary>
        /// 直前に送出した操作の説明です。
        /// </summary>
        public static string LastAction { get; private set; }

        /// <summary>
        /// 録画名です。
        /// </summary>
        public static string RecordingName { get; private set; }

        /// <summary>
        /// 録画上の概算フレーム番号です。
        /// </summary>
        public static int RecordingFrame { get; private set; }

        /// <summary>
        /// ランナー開始時に文脈を初期化し、前回 Play の値を残さないようにします。
        /// </summary>
        public static void BeginScenario(string scenarioName)
        {
            ScenarioName = scenarioName ?? string.Empty;
            StepIndex = 0;
            LastAction = string.Empty;
            RecordingName = string.Empty;
            RecordingFrame = 0;
        }

        /// <summary>
        /// ステップ境界で文脈を更新し、例外の直前操作を結果 JSON へ戻せるようにします。
        /// </summary>
        public static void SetStep(int stepIndex, string lastAction)
        {
            StepIndex = stepIndex;
            LastAction = lastAction ?? string.Empty;
        }

        /// <summary>
        /// 録画フレームと例外証拠を突き合わせるため、録画中だけ概算値を渡します。
        /// </summary>
        public static void SetRecording(string recordingName, int recordingFrame)
        {
            RecordingName = recordingName ?? string.Empty;
            RecordingFrame = recordingFrame;
        }

        /// <summary>
        /// 実行終了時に静的文脈を消し、別ランの例外へ誤って紐付けないようにします。
        /// </summary>
        public static void Clear()
        {
            ScenarioName = string.Empty;
            StepIndex = 0;
            LastAction = string.Empty;
            RecordingName = string.Empty;
            RecordingFrame = 0;
        }
    }
}
#endif
