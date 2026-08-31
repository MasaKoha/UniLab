using System;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UniLab.Input
{
    /// <summary>
    /// Android の戻るキーを監視する <see cref="IBackKeyInput"/> 実装。
    /// Input System では戻るキーが Escape として届くため、それを毎フレーム見る。
    /// 常駐オブジェクトに載せ、利用側の LifetimeScope で <see cref="IBackKeyInput"/> として登録する。
    /// </summary>
    public sealed class AndroidBackKeyInput : MonoBehaviour, IBackKeyInput, IDisposable
    {
        private readonly Subject<Unit> _onPressBackKey = new();
        private readonly CompositeDisposable _disposables = new();

        /// <inheritdoc/>
        public Observable<Unit> OnPressBackKey => _onPressBackKey;

        /// <inheritdoc/>
        public bool IsBlocked { get; private set; }

        /// <inheritdoc/>
        public void Initialize()
        {
            // Awake/Update に頼らず、所有者が明示的に呼ぶタイミングで監視を始める
            this.UpdateAsObservable()
                .Where(_ => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                .Subscribe(_ => GoBack())
                .AddTo(_disposables);
        }

        /// <inheritdoc/>
        public void SetBlock(bool block)
        {
            IsBlocked = block;
        }

        /// <summary>購読と Subject を破棄する。</summary>
        public void Dispose()
        {
            _disposables.Dispose();
            _onPressBackKey.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void GoBack()
        {
            if (IsBlocked)
            {
                return;
            }

            _onPressBackKey.OnNext(Unit.Default);
        }
    }
}
