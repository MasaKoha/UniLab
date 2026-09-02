#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 失敗理由を機械処理できる粒度で残し、後続の修正判断を画像確認に戻さないための JSON モデルです。
    /// </summary>
    [Serializable]
    public sealed class ScenarioExpectationFailure
    {
        /// <summary>
        /// どの期待値が落ちたかを種類で復元できるようにします。
        /// </summary>
        public string kind;

        /// <summary>
        /// 対象要素がある失敗を UI パスへ戻せるようにします。
        /// </summary>
        public string target;

        /// <summary>
        /// テキストやしきい値の失敗を期待値へ戻せるようにします。
        /// </summary>
        public string value;

        /// <summary>
        /// 人間がログだけで次の確認先を決められる短い説明です。
        /// </summary>
        public string message;

        /// <summary>
        /// 例外系の失敗をフォレンジック成果物へ直接つなぐためのパスです。
        /// </summary>
        public string evidencePath;
    }
}
#endif
