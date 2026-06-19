using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// IPopupService 実装。優先度付き待機列でポップアップ表示を直列化する。
    /// 重ね表示（スタック）は当面非対応で、常に 1 度に 1 つだけ表示する。VContainer に Singleton 登録する。
    /// </summary>
    public sealed class PopupService : IPopupService, IDisposable
    {
        private readonly IPopupViewProvider _viewProvider;
        private readonly List<PopupRequest> _waiting = new();
        private readonly ReactiveProperty<bool> _hasActivePopup = new(false);
        private PopupBase _currentPopup;

        /// <summary>表示中のポップアップがあるか。</summary>
        public ReadOnlyReactiveProperty<bool> HasActivePopup => _hasActivePopup;

        /// <summary>表示に用いる View 供給元を注入する。</summary>
        public PopupService(IPopupViewProvider viewProvider)
        {
            _viewProvider = viewProvider;
        }

        /// <summary>
        /// ポップアップを表示し結果を待つ。優先度順に直列化し、finally で必ずクローズ・解放してリークを防ぐ。
        /// </summary>
        public async UniTask<TResult> ShowAsync<TPopup, TResult>(
            IPopupParameter parameter, CancellationToken cancellationToken = default)
            where TPopup : PopupBase<TResult>
        {
            var request = new PopupRequest(parameter.Priority);
            Enqueue(request);
            TrySignalHead();

            // 基底クラス制約のみでは null 直接代入が NRT 警告となるため default を使う
            TPopup popup = default;
            try
            {
                // 待機列の先頭（自分の番）になるまで待つ
                await request.StartSignal.Task.AttachExternalCancellation(cancellationToken);

                popup = await _viewProvider.LoadAsync<TPopup>(cancellationToken);
                _currentPopup = popup;
                _hasActivePopup.Value = true;
                popup.Initialize(parameter);
                popup.gameObject.SetActive(true);
                await popup.OpenAsync();

                return await popup.GetResultAsync().AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                if (popup != null)
                {
                    // CloseAsync が中断例外を投げても、Release を必ず実行して View リークを防ぐ
                    try
                    {
                        await popup.CloseAsync();
                    }
                    finally
                    {
                        _viewProvider.Release(popup);
                    }
                }

                // popup と _currentPopup がともに null のときに誤一致して表示状態を消さないよう popup != null を前置する
                if (popup != null && _currentPopup == popup)
                {
                    _currentPopup = null;
                    _hasActivePopup.Value = false;
                }

                _waiting.Remove(request);
                TrySignalHead();
            }
        }

        /// <summary>表示中のポップアップをバックキー相当で閉じる。</summary>
        public async UniTask CloseTopAsync()
        {
            if (_currentPopup == null)
            {
                return;
            }

            var parameter = _currentPopup.Parameter;
            if (!parameter.EnableBackKey)
            {
                return;
            }

            if (parameter.CustomBackAsync != null)
            {
                await parameter.CustomBackAsync();
                return;
            }

            _currentPopup.OnClose();
        }

        /// <summary>HasActivePopup の購読リソースを破棄する。</summary>
        public void Dispose()
        {
            _hasActivePopup.Dispose();
        }

        private void Enqueue(PopupRequest request)
        {
            // 処理中（IsStarted）の要求は先頭に固定。未開始の中で優先度の高い順、同優先度は FIFO で挿入する
            var insertIndex = _waiting.Count;
            for (var index = 0; index < _waiting.Count; index++)
            {
                if (_waiting[index].IsStarted)
                {
                    continue;
                }

                if (_waiting[index].Priority < request.Priority)
                {
                    insertIndex = index;
                    break;
                }
            }

            _waiting.Insert(insertIndex, request);
        }

        private void TrySignalHead()
        {
            if (_waiting.Count == 0)
            {
                return;
            }

            var head = _waiting[0];
            if (head.IsStarted)
            {
                return;
            }

            // 先頭をアクティブ化し、ShowAsync の待機を解除して表示処理へ進ませる
            head.IsStarted = true;
            head.StartSignal.TrySetResult();
        }

        /// <summary>表示要求 1 件分の待機状態を保持する内部レコード。</summary>
        private sealed class PopupRequest
        {
            public PopupRequest(PopupPriority priority)
            {
                Priority = priority;
            }

            /// <summary>要求の優先度。挿入位置の決定に使う。</summary>
            public PopupPriority Priority { get; }

            /// <summary>表示処理を開始済みか。先頭固定・追い越し制御の判定に使う。</summary>
            public bool IsStarted { get; set; }

            /// <summary>自分の番が来たら完了するシグナル。</summary>
            public UniTaskCompletionSource StartSignal { get; } = new();
        }
    }
}
