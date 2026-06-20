using System;
using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 確認ポップアップ（ConfirmPopup）の表示内容・ボタン構成を指定するパラメータ。
    /// </summary>
    public class PopupParameter : IPopupParameter
    {
        /// <summary>ポップアップ上部に表示するタイトル。</summary>
        public string Title { get; set; }

        /// <summary>本文メッセージ。</summary>
        public string Message { get; set; }

        /// <summary>確認ボタンのラベル。</summary>
        public string ConfirmLabel { get; set; } = "OK";

        /// <summary>キャンセルボタンのラベル。null のときはキャンセルボタンを隠す。</summary>
        public string CancelLabel { get; set; }

        /// <summary>既存表示に重ねて即時表示するか。既定は重ねず優先度キューで直列表示する。</summary>
        public bool Stack { get; set; }

        // 確認ダイアログは通常優先度で扱う
        PopupPriority IPopupParameter.Priority => PopupPriority.Normal;

        // バックキーは Cancel として閉じる
        bool IPopupParameter.EnableBackKey => true;
        Func<UniTask> IPopupParameter.CustomBackAsync => null;

        // 誤操作防止のため確認ダイアログは背景タップで閉じない
        bool IPopupParameter.EnableBackgroundClose => false;
    }
}
