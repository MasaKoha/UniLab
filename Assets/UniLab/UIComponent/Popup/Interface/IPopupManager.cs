using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 確認ポップアップの表示と、確認 / キャンセル応答の待機を担う。後方互換用の薄い API。
    /// </summary>
    public interface IPopupManager
    {
        /// <summary>確認ポップアップを表示し、ユーザー応答を待つ。</summary>
        UniTask<PopupResult> ShowAsync(PopupParameter parameter, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 確認ポップアップの応答結果。
    /// </summary>
    public enum PopupResult
    {
        /// <summary>未確定（既定値）。</summary>
        None = 0,

        /// <summary>確認（OK）。</summary>
        Confirm,

        /// <summary>キャンセル。</summary>
        Cancel,
    }

    /// <summary>
    /// 確認ポップアップの表示内容・ボタン構成を指定するパラメータ。
    /// IPopupParameter を実装し、ポップアップ基盤へそのまま渡せる。
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

        // 確認ダイアログは通常優先度で扱う
        PopupPriority IPopupParameter.Priority => PopupPriority.Normal;

        // バックキーは Cancel として閉じる
        bool IPopupParameter.EnableBackKey => true;
        Func<UniTask> IPopupParameter.CustomBackAsync => null;

        // 誤操作防止のため確認ダイアログは背景タップで閉じない
        bool IPopupParameter.EnableBackgroundClose => false;
    }
}
