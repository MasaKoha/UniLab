using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// UI 自動巡回シナリオ。JSON から JsonUtility で読み込む。
    /// </summary>
    [Serializable]
    public sealed class UiScenario
    {
        /// <summary>
        /// 結果ファイル名や性能レポート名へ意図を残すための任意名です。
        /// </summary>
        public string name;

        /// <summary>
        /// 撮影の出力先ディレクトリ。空なら DebugOutputPath の既定配下を使う。
        /// </summary>
        public string outputDirectory;

        /// <summary>
        /// 失敗後も証拠を集める既定を保ちつつ、早期停止したい検証だけ切り替えるための指定です。
        /// </summary>
        public bool stopOnFail;

        /// <summary>
        /// 録画外でも入力可視化を出したいシナリオを明示できるようにします。
        /// </summary>
        public bool inputOverlay;

        /// <summary>
        /// JsonUtility の bool 未指定問題を補い、false 明示時だけ録画既定表示を抑制するための内部情報です。
        /// </summary>
        [NonSerialized]
        public bool inputOverlaySpecified;

        /// <summary>
        /// シナリオ全体の性能をステップ境界付きで残すための指定です。
        /// </summary>
        public bool recordPerformance;

        /// <summary>
        /// 07 の実行結果パスを 02 の JSON へ受け渡すための欄です。
        /// </summary>
        public string visualRegression;

        /// <summary>
        /// 入力記録を replay 資産へ昇格し、05 の再現確認へ同じ入口から接続するための指定です。
        /// </summary>
        public bool recordInputs;

        /// <summary>
        /// 既存 replay を先に流し、修正後確認をシナリオ入口へ統合するための指定です。
        /// </summary>
        public string replay;

        /// <summary>
        /// 上から順に実行するシナリオステップ列。
        /// </summary>
        public UiScenarioStep[] steps;
    }
}
#endif
