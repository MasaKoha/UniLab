using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// 失敗ステップの証拠パスだけ差し替えても、元の判定内容を保ったまま再保存できるようにする。
    /// </summary>
    [Serializable]
    public sealed class RunArchiveScenarioStepResult
    {
        /// <summary>
        /// 元シナリオ位置との対応を保つため、ステップ番号を保持する。
        /// </summary>
        public int index;

        /// <summary>
        /// 何を押した結果かを詳細画面に出せるよう、送出対象を保持する。
        /// </summary>
        public string submit;

        /// <summary>
        /// pass / fail をステップ単位で読めるようにする。
        /// </summary>
        public string status;

        /// <summary>
        /// 応答時間の異常を結果だけで確認できるよう、待機秒数を保持する。
        /// </summary>
        public float waitedSeconds;

        /// <summary>
        /// 失敗理由を保持し、証拠画像だけでは伝わらない機械判定を残す。
        /// </summary>
        public RunArchiveScenarioFailure[] failures;

        /// <summary>
        /// ラン配下へ移した証拠ファイルの相対パスを保持する。
        /// </summary>
        public RunArchiveStepEvidence evidence;
    }
}
