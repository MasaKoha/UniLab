using R3;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 「閉じる」入力の供給元。Android の戻るキー、パッドの Ⓑ、Esc など、
    /// 何を閉じる操作とみなすかはプロジェクトごとに異なるため差し替え可能にする。
    /// </summary>
    public interface IPopupBackKeySource
    {
        /// <summary>最前面のポップアップを閉じる操作が行われたときに発火する。</summary>
        Observable<Unit> OnBack { get; }
    }
}
