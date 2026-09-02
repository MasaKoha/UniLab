#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// シナリオの 1 ステップ。指定されたフィールドだけが実行される。
    /// </summary>
    [Serializable]
    public sealed class UiScenarioStep
    {
        /// <summary>
        /// submit を送る GameObject 名。親名/子名 のパス指定にも対応する。空なら操作しない。
        /// </summary>
        public string submit;

        /// <summary>
        /// このシーン名がロード済みになるまで待つ。空なら待機しない。
        /// </summary>
        public string waitScene;

        /// <summary>
        /// 撮影ファイル名。拡張子は付けない。空なら撮影しない。
        /// </summary>
        public string capture;

        /// <summary>
        /// true のとき UiLayoutAuditor を実行し JSON を保存する。
        /// </summary>
        public bool audit;

        /// <summary>
        /// true のとき、このステップの開始時に録画を開始する。
        /// </summary>
        public bool recordStart;

        /// <summary>録画のフレームレート。0 以下のとき既定値（30）を使う。recordStart と同じステップに書く。</summary>
        public int recordFps;

        /// <summary>
        /// 空でないとき、このステップの完了時に録画を停止し、この名前で確定する。
        /// </summary>
        public string recordStop;

        /// <summary>
        /// 整定待ちフレーム数。0 以下のとき既定値を使う。
        /// </summary>
        public int settleFrames;
    }
}
#endif
