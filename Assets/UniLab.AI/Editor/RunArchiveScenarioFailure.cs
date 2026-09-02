using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// 失敗理由の配列を落とさず再保存し、証拠ファイルのパスだけ差し替えても判定根拠を保持できるようにする。
    /// </summary>
    [Serializable]
    public sealed class RunArchiveScenarioFailure
    {
        /// <summary>
        /// どの評価種別で落ちたかを残し、ギャラリー側が文脈を付けやすくする。
        /// </summary>
        public string kind;

        /// <summary>
        /// 要素パス系の失敗を詳細なしでも読めるよう、対象を保持する。
        /// </summary>
        public string target;

        /// <summary>
        /// テキスト一致系の失敗を一覧で説明できるよう、比較値を保持する。
        /// </summary>
        public string value;

        /// <summary>
        /// 人間が詳細ロジックを知らなくても判断できるよう、メッセージを維持する。
        /// </summary>
        public string message;
    }
}
