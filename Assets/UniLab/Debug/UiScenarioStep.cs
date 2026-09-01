using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.Diagnostics
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
        /// 整定待ちフレーム数。0 以下のとき既定値を使う。
        /// </summary>
        public int settleFrames;
    }
}
#endif
