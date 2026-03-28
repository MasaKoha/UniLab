using System;
using System.Collections.Generic;
using R3;

namespace UniLab.UI
{
    /// <summary>
    /// Static factory and registry for input blocks. Tracks all active blocks and fires show/hide observables.
    /// </summary>
    public static class InputBlockManager
    {
        private static ulong _blockingIdCounter = 0;
        private static readonly Dictionary<ulong, IDisposable> _inputBlocks = new();

        /// <summary>True while any input block is active.</summary>
        public static bool BlockedInput => _inputBlocks.Count > 0;

        /// <summary>Fires when a loading input block is created.</summary>
        private static readonly Subject<Unit> _onShowLoading = new();
        public static Observable<Unit> OnShowLoading => _onShowLoading;

        /// <summary>Fires when a loading input block is disposed.</summary>
        private static readonly Subject<Unit> _onHideLoading = new();
        public static Observable<Unit> OnHideLoading => _onHideLoading;

        /// <summary>Fires when any input block (loading or plain) is created.</summary>
        private static readonly Subject<Unit> _onShow = new();
        public static Observable<Unit> OnShow => _onShow;

        /// <summary>Fires when any input block (loading or plain) is disposed.</summary>
        private static readonly Subject<Unit> _onHide = new();
        public static Observable<Unit> OnHide => _onHide;

        /// <summary>Creates an input block that also triggers loading overlay observables.</summary>
        public static LoadingInputBlock CreateInputBlockWithLoading()
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

        /// <summary>Creates a plain input block without a loading indicator.</summary>
        public static InputBlock CreateInputBlock()
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

        /// <summary>
        /// Immediately disposes and removes all active input blocks.
        /// Use only in error recovery paths — normal flow should dispose handles individually.
        /// </summary>
        public static void ForceReleaseAllInputBlocks()
        {
            foreach (var block in _inputBlocks.Values)
            {
                block.Dispose();
            }

            _inputBlocks.Clear();
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
