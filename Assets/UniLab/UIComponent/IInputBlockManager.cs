using System;
using R3;

namespace UniLab.UI
{
    /// <summary>
    /// 入力ブロックの発行と集計。ローディング表示や画面遷移中に UI 操作を止めたい側が
    /// ブロックを取得し、Dispose で解除する。利用側の LifetimeScope で Singleton 登録する。
    /// </summary>
    public interface IInputBlockManager
    {
        /// <summary>1 つでもブロックが有効な間 true。</summary>
        bool BlockedInput { get; }

        /// <summary>ローディング付きブロックが発行されたときに発火する。</summary>
        Observable<Unit> OnShowLoading { get; }

        /// <summary>ローディング付きブロックが解除されたときに発火する。</summary>
        Observable<Unit> OnHideLoading { get; }

        /// <summary>種類を問わずブロックが発行されたときに発火する。</summary>
        Observable<Unit> OnShow { get; }

        /// <summary>種類を問わずブロックが解除されたときに発火する。</summary>
        Observable<Unit> OnHide { get; }

        /// <summary>ローディング表示を伴う入力ブロックを発行する。</summary>
        LoadingInputBlock CreateInputBlockWithLoading();

        /// <summary>ローディング表示を伴わない入力ブロックを発行する。</summary>
        InputBlock CreateInputBlock();

        /// <summary>全ブロックを強制解除する。エラー復帰用で、通常は個々のハンドルを Dispose する。</summary>
        void ForceReleaseAllInputBlocks();
    }
}
