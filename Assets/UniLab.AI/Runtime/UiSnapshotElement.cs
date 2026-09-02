#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 画面上の 1 要素を表す JSON モデルです。
    /// クリック可否や表示テキストを 1 行で読める形へ落とし込みます。
    /// </summary>
    [Serializable]
    public sealed class UiSnapshotElement
    {
        /// <summary>
        /// ルートからの階層パスです。
        /// 人間向け名称が重複しても後段の操作対象を一意に戻せるようにします。
        /// </summary>
        public string path;

        /// <summary>
        /// 要素名です。
        /// 圧縮テキストではパスより短く読める識別子として使います。
        /// </summary>
        public string name;

        /// <summary>
        /// 意味上の種別です。
        /// コンポーネント実装差を AI へ露出させず判断に必要な語彙へ寄せます。
        /// </summary>
        public string kind;

        /// <summary>
        /// 人が目で読むラベルです。
        /// ボタン名や本文を画像 OCR なしで取れるようにします。
        /// </summary>
        public string label;

        /// <summary>
        /// 画面座標の矩形です。
        /// レイアウト変化や遮蔽位置を後段で解析できるようにします。
        /// </summary>
        public float[] rect;

        /// <summary>
        /// 操作可能判定です。
        /// 押せない理由の第一段階を即座に読めるようにします。
        /// </summary>
        public bool interactable;

        /// <summary>
        /// 最前面でなかった場合の遮蔽物名です。
        /// 画面上に見えていても押せない要因を画像なしで示します。
        /// </summary>
        public string blockedBy;

        /// <summary>
        /// 現在フォーカスされている要素かどうかです。
        /// キーボードやゲームパッド UI の現在位置を復元するためです。
        /// </summary>
        public bool focused;

        /// <summary>
        /// 値を持つ UI の現在値です。
        /// トグルや入力欄の状態をラベルと分離して表すためです。
        /// </summary>
        public string value;
    }
}
#endif
