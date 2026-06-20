using R3;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 全ポップアップ共通の暗幕（ディマー）。各ポップアップが個別に背景を持たず、1 枚を最前面ポップアップの背後に敷く。
    /// PopupService が表示・非表示と背景タップ購読に用いる。
    /// </summary>
    public interface IPopupDimmer
    {
        /// <summary>暗幕タップ通知。PopupService が背景タップによる閉じ判定に購読する。</summary>
        Observable<Unit> OnClick { get; }

        /// <summary>暗幕を表示し、対象ポップアップの直下（背後）へ配置する。</summary>
        void Show(Transform popupTransform);

        /// <summary>暗幕を隠す。</summary>
        void Hide();
    }
}
