using System;
using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// ポップアップ表示時の挙動を表すパラメータ。各ポップアップ生成時に Initialize へ渡される。
    /// </summary>
    public interface IPopupParameter
    {
        /// <summary>表示要求の優先度。PopupService のキューイング順を決める。</summary>
        PopupPriority Priority { get; }

        /// <summary>バックキーに反応して閉じるか。</summary>
        bool EnableBackKey { get; }

        /// <summary>バックキー時に実行する処理。null の場合は既定の閉じ動作を行う。</summary>
        Func<UniTask> CustomBackAsync { get; }

        /// <summary>背景タップで閉じられるか。</summary>
        bool EnableBackgroundClose { get; }

        /// <summary>
        /// 現在の最前面に重ねて即時表示するか。true は待機列を介さずスタック表示する。
        /// false（既定）は優先度キューで 1 枚ずつ直列表示する。
        /// </summary>
        bool Stack { get; }
    }
}
