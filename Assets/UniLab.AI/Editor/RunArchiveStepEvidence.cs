using System;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// 失敗証拠の参照先を 1 箇所へまとめ、RunArchive への再配置時に更新対象を限定できるようにする。
    /// </summary>
    [Serializable]
    public sealed class RunArchiveStepEvidence
    {
        /// <summary>
        /// 失敗時スクリーンショットの導線を保持し、詳細画面の先頭証拠へ直行できるようにする。
        /// </summary>
        public string capture;

        /// <summary>
        /// 構造化状態の導線を保持し、圧縮テキスト生成元を見失わないようにする。
        /// </summary>
        public string snapshot;
    }
}
