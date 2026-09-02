#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 2 つの UI スナップショットの差分です。
    /// 操作結果が空振りかどうかを画像比較なしで判断できるようにします。
    /// </summary>
    [Serializable]
    public sealed class UiSnapshotDiff
    {
        /// <summary>
        /// 新しく出現した要素パスです。
        /// モーダルや遷移後 UI の追加を即座に見分けるためです。
        /// </summary>
        public string[] addedPaths;

        /// <summary>
        /// 消えた要素パスです。
        /// 画面切替や閉じたモーダルを明示するためです。
        /// </summary>
        public string[] removedPaths;

        /// <summary>
        /// 残存要素の変更一覧です。
        /// 何が変化したかを局所的に把握しやすくするためです。
        /// </summary>
        public UiSnapshotChange[] changed;

        /// <summary>
        /// 変化前フォーカスです。
        /// キー操作で選択位置だけが動いたケースを拾うためです。
        /// </summary>
        public string focusedBefore;

        /// <summary>
        /// 変化後フォーカスです。
        /// キー操作で選択位置だけが動いたケースを拾うためです。
        /// </summary>
        public string focusedAfter;

        /// <summary>
        /// 変化前シーンです。
        /// シーン跨ぎの遷移を明示するためです。
        /// </summary>
        public string sceneBefore;

        /// <summary>
        /// 変化後シーンです。
        /// シーン跨ぎの遷移を明示するためです。
        /// </summary>
        public string sceneAfter;

        /// <summary>
        /// 変化が無かったかどうかです。
        /// クリックしたのに何も起きない不具合を一発で検知するためです。
        /// </summary>
        public bool isEmpty;
    }
}
#endif
