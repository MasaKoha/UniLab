using R3;

namespace UniLab.Input
{
    /// <summary>
    /// 「戻る」入力の契約。シーンマネージャが履歴を戻す判断に使う。
    /// 実装を DI で差し替えられるようにし、静的シングルトン経由の取得を不要にする。
    /// </summary>
    public interface IBackKeyInput
    {
        /// <summary>戻る操作が行われたときに発火する。<see cref="IsBlocked"/> の間は発火しない。</summary>
        Observable<Unit> OnPressBackKey { get; }

        /// <summary>戻る操作を受け付けない状態か。シーン遷移中などに true になる。</summary>
        bool IsBlocked { get; }

        /// <summary>戻る操作の受付を切り替える。シーンマネージャが遷移の前後で呼ぶ。</summary>
        void SetBlock(bool block);

        /// <summary>入力の監視を開始する。所有者が起動時に一度だけ呼ぶ。</summary>
        void Initialize();
    }
}
