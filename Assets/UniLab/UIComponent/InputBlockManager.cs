using System;
using System.Collections.Generic;
using R3;

namespace UniLab.UI
{
    /// <summary>
    /// <see cref="IInputBlockManager"/> の実装。発行中のブロックを ID で管理し、表示／解除の Observable を流す。
    /// 静的クラスではなくインスタンスにしているのは、状態を DI の寿命に閉じ込めテストで差し替えられるようにするため。
    /// </summary>
    public sealed class InputBlockManager : IInputBlockManager, IDisposable
    {
        private ulong _blockingIdCounter = 0;
        private readonly Dictionary<ulong, IDisposable> _inputBlocks = new();
        private readonly Subject<Unit> _onShowLoading = new();
        private readonly Subject<Unit> _onHideLoading = new();
        private readonly Subject<Unit> _onShow = new();
        private readonly Subject<Unit> _onHide = new();

        /// <inheritdoc/>
        public bool BlockedInput => _inputBlocks.Count > 0;

        /// <inheritdoc/>
        public Observable<Unit> OnShowLoading => _onShowLoading;

        /// <inheritdoc/>
        public Observable<Unit> OnHideLoading => _onHideLoading;

        /// <inheritdoc/>
        public Observable<Unit> OnShow => _onShow;

        /// <inheritdoc/>
        public Observable<Unit> OnHide => _onHide;

        /// <inheritdoc/>
        public LoadingInputBlock CreateInputBlockWithLoading()
        {
            _onShowLoading.OnNext(Unit.Default);
            _onShow.OnNext(Unit.Default);
            var blockingId = _blockingIdCounter++;
            var block = new LoadingInputBlock(() =>
            {
                _inputBlocks.Remove(blockingId);
                _onHideLoading.OnNext(Unit.Default);
                _onHide.OnNext(Unit.Default);
            })
            {
                BlockingId = blockingId
            };
            _inputBlocks[blockingId] = block;
            return block;
        }

        /// <inheritdoc/>
        public InputBlock CreateInputBlock()
        {
            _onShow.OnNext(Unit.Default);
            var blockingId = _blockingIdCounter++;
            var block = new InputBlock(() =>
            {
                _inputBlocks.Remove(blockingId);
                _onHide.OnNext(Unit.Default);
            })
            {
                BlockingId = blockingId
            };
            _inputBlocks[blockingId] = block;
            return block;
        }

        /// <inheritdoc/>
        public void ForceReleaseAllInputBlocks()
        {
            // Dispose 内でコレクションが変わるため、複製を回す
            foreach (var block in new List<IDisposable>(_inputBlocks.Values))
            {
                block.Dispose();
            }

            _inputBlocks.Clear();
        }

        /// <summary>Subject を破棄する。</summary>
        public void Dispose()
        {
            _onShowLoading.Dispose();
            _onHideLoading.Dispose();
            _onShow.Dispose();
            _onHide.Dispose();
        }
    }

    /// <summary>
    /// Base class for input block handles. Dispose to release the block.
    /// </summary>
    public abstract class InputBlockBase : IDisposable
    {
        /// <summary>Unique ID assigned by InputBlockManager to track this block.</summary>
        public ulong BlockingId;

        private readonly Action _onDispose;

        protected InputBlockBase(Action onDispose)
        {
            _onDispose = onDispose;
        }

        /// <summary>Releases this input block and fires the associated hide observables.</summary>
        public void Dispose()
        {
            _onDispose.Invoke();
        }
    }

    /// <summary>Input block without a loading indicator.</summary>
    public sealed class InputBlock : InputBlockBase
    {
        public InputBlock(Action onDispose) : base(onDispose)
        {
        }
    }

    /// <summary>Input block that also signals loading overlay show/hide observables.</summary>
    public sealed class LoadingInputBlock : InputBlockBase
    {
        public LoadingInputBlock(Action onDispose) : base(onDispose)
        {
        }
    }
}
