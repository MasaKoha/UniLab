using System;
using System.Collections.Generic;
using R3;

namespace UniLab.Common.Display
{
    public static class InputBlockManager
    {
        private static ulong _blockingIdCounter = 0;
        private static readonly Dictionary<ulong, IDisposable> _inputBlocks = new();
        public static bool BlockedInput => _inputBlocks.Count > 0;

        private static readonly Subject<Unit> _onShowLoading = new();
        public static Observable<Unit> OnShowLoading => _onShowLoading;
        private static readonly Subject<Unit> _onHideLoading = new();
        public static Observable<Unit> OnHideLoading => _onHideLoading;

        private static readonly Subject<Unit> _onShow = new();
        public static Observable<Unit> OnShow => _onShow;
        private static readonly Subject<Unit> _onHide = new();
        public static Observable<Unit> OnHide => _onHide;

        public static LoadingInputBlock CreateInputBlockWithLoading()
        {
            _onShowLoading.OnNext(Unit.Default);
            _onShow.OnNext(Unit.Default);
            var block = new LoadingInputBlock(() =>
            {
                _onHideLoading.OnNext(Unit.Default);
                _onHide.OnNext(Unit.Default);
            })
            {
                BlockingId = _blockingIdCounter++
            };
            _inputBlocks[block.BlockingId] = block;
            return block;
        }

        public static InputBlock CreateInputBlock()
        {
            _onShow.OnNext(Unit.Default);
            var block = new InputBlock(() => { _onHide.OnNext(Unit.Default); })
            {
                BlockingId = _blockingIdCounter++
            };
            _inputBlocks[block.BlockingId] = block;
            return block;
        }

        public static void ForceReleaseAllInputBlocks()
        {
            foreach (var block in _inputBlocks.Values)
            {
                block.Dispose();
            }

            _inputBlocks.Clear();
        }
    }

    public class InputBlock : IDisposable
    {
        public ulong BlockingId;
        private readonly Action _onDispose;

        public InputBlock(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose.Invoke();
        }
    }

    public class LoadingInputBlock : IDisposable
    {
        public ulong BlockingId;
        private readonly Action _onDispose;

        public LoadingInputBlock(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose.Invoke();
        }
    }
}