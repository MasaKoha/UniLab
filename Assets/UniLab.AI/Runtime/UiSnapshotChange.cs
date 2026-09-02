#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 既存要素のどの項目が変わったかを表す差分行です。
    /// 操作前後で何が変化したかだけを短く読めるようにします。
    /// </summary>
    [Serializable]
    public sealed class UiSnapshotChange
    {
        /// <summary>
        /// 変化した要素のパスです。
        /// 差分を UI 上の対象へ戻すために保持します。
        /// </summary>
        public string path;

        /// <summary>
        /// 変化したフィールド名です。
        /// ラベル変化か操作可否変化かを一目で判別できるようにします。
        /// </summary>
        public string field;

        /// <summary>
        /// 変化前の値です。
        /// 実際に何が失われたかを後追いで確認できるようにします。
        /// </summary>
        public string before;

        /// <summary>
        /// 変化後の値です。
        /// 実際に何へ変わったかを画像なしで読めるようにします。
        /// </summary>
        public string after;
    }
}
#endif
