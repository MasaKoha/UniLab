#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 1 時点の UI 状態を機械処理向けに固定化した JSON モデルです。
    /// 画像を開かずに画面の意味を読めるよう、必要情報だけを平坦化します。
    /// </summary>
    [Serializable]
    public sealed class UiSnapshotDocument
    {
        /// <summary>
        /// 取得時刻です。
        /// 動画やログと後から突き合わせるため ISO 8601 で保持します。
        /// </summary>
        public string capturedAt;

        /// <summary>
        /// 取得フレームです。
        /// フレーム依存の差異を再確認しやすくするため保持します。
        /// </summary>
        public int frame;

        /// <summary>
        /// アクティブシーン名です。
        /// 同名 UI が複数シーンにある場合の文脈を失わないためです。
        /// </summary>
        public string activeScene;

        /// <summary>
        /// 取得時の画面幅です。
        /// 座標解釈を後段で復元できるようにします。
        /// </summary>
        public int screenWidth;

        /// <summary>
        /// 取得時の画面高さです。
        /// 座標解釈を後段で復元できるようにします。
        /// </summary>
        public int screenHeight;

        /// <summary>
        /// 現在フォーカスされている要素のパスです。
        /// キーボード操作や自動選択の文脈を画像なしで追えるようにします。
        /// </summary>
        public string focusedPath;

        /// <summary>
        /// 画面上の意味ある UI 要素一覧です。
        /// 全階層ではなく可視で判断材料になるものだけを並べます。
        /// </summary>
        public UiSnapshotElement[] elements;

        /// <summary>
        /// ゲーム固有状態の平坦な一覧です。
        /// UI に出ていない状態も補助的に読めるよう辞書を配列へ展開します。
        /// </summary>
        public UiSnapshotGameEntry[] game;
    }
}
#endif
