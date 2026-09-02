#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 例外時の文脈を保存時点で固定し、次フレーム収集中にランナー状態が進んでも証拠をずらさないための JSON モデルです。
    /// </summary>
    [Serializable]
    public sealed class ForensicsContextSnapshot
    {
        /// <summary>
        /// 収集したフレーム番号です。
        /// </summary>
        public int frame;

        /// <summary>
        /// 発生から 1 フレーム後の絵であることを後段が判断できるようにします。
        /// </summary>
        public bool capturedNextFrame;

        /// <summary>
        /// 実時間です。
        /// </summary>
        public float realtimeSinceStartup;

        /// <summary>
        /// アクティブシーン名です。
        /// </summary>
        public string activeScene;

        /// <summary>
        /// 実行中シナリオ名です。
        /// </summary>
        public string scenario;

        /// <summary>
        /// 実行中ステップ番号です。
        /// </summary>
        public int stepIndex;

        /// <summary>
        /// 直前操作です。
        /// </summary>
        public string lastAction;

        /// <summary>
        /// 録画名です。
        /// </summary>
        public string recordingName;

        /// <summary>
        /// 録画上の概算フレーム番号です。
        /// </summary>
        public int recordingFrame;

        /// <summary>
        /// 同じスタックの再発回数です。
        /// </summary>
        public int repeatCount;
    }
}
#endif
