using System;
using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup.Sample
{
    /// <summary>
    /// 報酬ポップアップへ表示内容と動作設定を渡すパラメータ。
    /// </summary>
    public sealed class RewardPopupParameter : IPopupParameter
    {
        /// <summary>表示する報酬名。</summary>
        public string RewardName { get; set; }

        /// <summary>表示する報酬数。</summary>
        public int Amount { get; set; }

        /// <summary>表示要求の優先度。</summary>
        public PopupPriority Priority { get; set; } = PopupPriority.Normal;

        /// <summary>バックキーで閉じることを許可する。</summary>
        public bool EnableBackKey => true;

        /// <summary>バックキーでは基盤の既定クローズ処理を使う。</summary>
        public Func<UniTask> CustomBackAsync => null;

        /// <summary>背景タップで閉じることを許可する。</summary>
        public bool EnableBackgroundClose => true;
    }
}
