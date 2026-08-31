using R3;

namespace UniLab.Input
{
    /// <summary>
    /// 「戻る」入力を持たないプラットフォーム（PC など）向けの <see cref="IBackKeyInput"/> 実装。
    /// 何も監視せず、<see cref="OnPressBackKey"/> は一度も発火しない。
    /// MonoBehaviour ではないため、シーンに空のオブジェクトを置く必要がない。
    /// 戻る操作をパッドの Ⓑ などに割り当てたい場合は、この代わりに独自実装を登録する。
    /// </summary>
    public sealed class NullBackKeyInput : IBackKeyInput
    {
        /// <inheritdoc/>
        public Observable<Unit> OnPressBackKey => Observable.Never<Unit>();

        /// <inheritdoc/>
        public bool IsBlocked { get; private set; }

        /// <summary>監視するものが無いため何もしない。</summary>
        public void Initialize()
        {
        }

        /// <summary>
        /// 発火しないため実質意味を持たないが、シーンマネージャが遷移中に呼ぶ契約のため状態は保持する。
        /// </summary>
        public void SetBlock(bool block)
        {
            IsBlocked = block;
        }
    }
}
