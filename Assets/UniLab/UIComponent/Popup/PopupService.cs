using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.UI.Focus;
using UnityEngine.UI;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// IPopupService 実装。Stack=false は優先度付き待機列で 1 枚ずつ直列表示し、
    /// Stack=true は待機列を介さず現在の最前面へ即時に重ねる（オプトイン・スタック）。
    /// 共通暗幕は常に最前面ポップアップの背後へ移動し、タップで最前面のみを閉じる。VContainer に Singleton 登録する。
    /// </summary>
    public sealed class PopupService : IPopupService, IDisposable
    {
        private readonly IPopupViewProvider _viewProvider;
        private readonly IPopupDimmer _dimmer;
        private readonly List<PopupRequest> _waiting = new();
        private readonly List<PopupBase> _stack = new();
        private readonly ReactiveProperty<bool> _hasActivePopup = new(false);
        private readonly IDisposable _dimmerClickSubscription;

        /// <summary>パッド操作時のフォーカス積み先。未接続ならフォーカス制御を行わない。</summary>
        private IFocusNavigator _focusNavigator;

        /// <summary>表示中のポップアップが 1 つでもあるか。</summary>
        public ReadOnlyReactiveProperty<bool> HasActivePopup => _hasActivePopup;

        /// <summary>
        /// 表示に用いる View 供給元と、任意の共通暗幕を注入する。
        /// dimmer 未指定時は各ポップアップが個別背景を持つ従来挙動になる。
        /// </summary>
        public PopupService(IPopupViewProvider viewProvider, IPopupDimmer dimmer = null)
        {
            _viewProvider = viewProvider;
            _dimmer = dimmer;
            // 暗幕タップは常に最前面へ集約する。スタック時に下位まで閉じないよう購読は 1 つに限定する
            _dimmerClickSubscription = _dimmer?.OnClick.Subscribe(_ => OnDimmerClicked());
        }

        /// <summary>
        /// ポップアップを表示し結果を待つ。Stack 指定は即時に重ね、非指定は優先度順に直列化する。
        /// いずれもキャンセル・例外時に finally で必ずクローズ・解放してリークを防ぐ。
        /// </summary>
        public async UniTask<TResult> ShowAsync<TPopup, TResult>(
            IPopupParameter parameter, CancellationToken cancellationToken = default)
            where TPopup : PopupBase<TResult>
        {
            // スタック指定は待機列を介さず、現在の最前面に即時に重ねる
            if (parameter.Stack)
            {
                return await PresentAsync<TPopup, TResult>(parameter, cancellationToken);
            }

            // ベース表示は優先度順に直列化する。自分の番が来るまで待機列で待つ
            var request = new PopupRequest(parameter.Priority);
            Enqueue(request);
            TrySignalHead();
            try
            {
                await request.StartSignal.Task.AttachExternalCancellation(cancellationToken);
                return await PresentAsync<TPopup, TResult>(parameter, cancellationToken);
            }
            finally
            {
                // 自分の処理が完全に終わってから待機列を抜け、次のベースへ番を渡す
                _waiting.Remove(request);
                TrySignalHead();
            }
        }

        /// <summary>表示中の最前面ポップアップをバックキー相当で閉じる。表示中でなければ何もしない。</summary>
        public async UniTask CloseTopAsync()
        {
            var top = TopPopup();
            if (top == null)
            {
                return;
            }

            var parameter = top.Parameter;
            if (!parameter.EnableBackKey)
            {
                return;
            }

            if (parameter.CustomBackAsync != null)
            {
                await parameter.CustomBackAsync();
                return;
            }

            top.OnClose();
        }

        /// <summary>
        /// 表示中の全ポップアップを最前面から順に閉じ、全て閉じ終わる（スタックが空になる）まで待つ。
        /// EnableBackKey に依らず強制クローズする。待機列の未表示要求は対象外。
        /// </summary>
        public async UniTask CloseAllAsync()
        {
            // OnClose は結果確定のみで、実際の除去は各 PresentAsync の finally で行われる。
            // よってこのループ中に _stack は変化しない。最前面から順に閉じ要求を出す
            for (var index = _stack.Count - 1; index >= 0; index--)
            {
                var popup = _stack[index];
                if (popup != null)
                {
                    popup.OnClose();
                }
            }

            // 全 PresentAsync のクローズ・解放完了（スタックが空）まで待つ
            await UniTask.WaitWhile(() => _stack.Count > 0);
        }

        /// <inheritdoc/>
        public void AttachFocusNavigator(IFocusNavigator focusNavigator)
        {
            _focusNavigator = focusNavigator;
        }

        /// <inheritdoc/>
        public void DetachFocusNavigator()
        {
            _focusNavigator = null;
        }

        /// <summary>暗幕タップ購読と HasActivePopup の購読リソースを破棄する。</summary>
        public void Dispose()
        {
            _dimmerClickSubscription?.Dispose();
            _hasActivePopup.Dispose();
        }

        // --- 表示処理（スタック / ベース共通） ---

        /// <summary>
        /// 表示〜結果待ち〜クローズ〜解放の共通処理。スタックへ積み、暗幕を最前面へ移動して表示する。
        /// </summary>
        private async UniTask<TResult> PresentAsync<TPopup, TResult>(
            IPopupParameter parameter, CancellationToken cancellationToken)
            where TPopup : PopupBase<TResult>
        {
            // 基底クラス制約のみでは null 直接代入が NRT 警告となるため default を使う
            TPopup popup = default;

            // 閉じたあとに元の位置へフォーカスを戻すため、奪う前の選択を控える
            var previousSelected = _focusNavigator?.CurrentSelected;
            FocusGrid focusGrid = null;
            try
            {
                popup = await _viewProvider.LoadAsync<TPopup>(cancellationToken);
                popup.Initialize(parameter);
                popup.gameObject.SetActive(true);
                _stack.Add(popup);
                RefreshState();
                await popup.OpenAsync();

                // 開くアニメーションの完了後に積む。再生中は PopupBase が操作不可にしているため
                focusGrid = PushFocus(popup);

                return await popup.GetResultAsync().AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                // グリッドは View の解放より先に降ろす。破棄済みの Selectable を握ったまま方向解決が走るのを防ぐ
                PopFocus(focusGrid, previousSelected);

                if (popup != null)
                {
                    // CloseAsync が中断例外を投げても、Release を必ず実行して View リークを防ぐ
                    try
                    {
                        await popup.CloseAsync();
                    }
                    finally
                    {
                        _stack.Remove(popup);
                        RefreshState();
                        _viewProvider.Release(popup);
                    }
                }
            }
        }

        /// <summary>
        /// ポップアップのフォーカスグリッドを積み、初期フォーカスを当てる。
        /// FocusNavigator 未接続、またはポップアップがグリッドを返さない場合は何もしない。
        /// </summary>
        private FocusGrid PushFocus(PopupBase popup)
        {
            if (_focusNavigator == null)
            {
                return null;
            }

            var grid = popup.BuildFocusGrid();
            if (grid == null)
            {
                return null;
            }

            _focusNavigator.PushGrid(grid);

            var initialFocus = popup.InitialFocus;
            if (initialFocus != null)
            {
                _focusNavigator.SetSelected(initialFocus);
            }

            return grid;
        }

        /// <summary>積んだグリッドを降ろし、ポップアップを開く前の選択へ戻す。</summary>
        private void PopFocus(FocusGrid grid, Selectable previousSelected)
        {
            if (_focusNavigator == null || grid == null)
            {
                return;
            }

            _focusNavigator.PopGrid(grid);

            // 元の要素が破棄されている（シーン遷移を挟んだ等）場合は復元しない
            if (previousSelected != null)
            {
                _focusNavigator.SetSelected(previousSelected);
            }
        }

        // 暗幕タップは最前面のみを閉じる。背景タップ許可時だけ反応する
        private void OnDimmerClicked()
        {
            var top = TopPopup();
            if (top != null && top.Parameter.EnableBackgroundClose)
            {
                top.OnClose();
            }
        }

        // スタックの増減に応じて表示中フラグと暗幕位置を更新する
        private void RefreshState()
        {
            _hasActivePopup.Value = _stack.Count > 0;
            if (_dimmer == null)
            {
                return;
            }

            var top = TopPopup();
            if (top == null)
            {
                _dimmer.Hide();
            }
            else
            {
                // 暗幕を最前面ポップアップの直下へ移動する。下位ポップアップは暗幕で減光される
                _dimmer.Show(top.transform);
            }
        }

        // 最前面ポップアップ。空なら null。破棄済み（Unity の == 判定）も null として扱う
        private PopupBase TopPopup()
        {
            if (_stack.Count == 0)
            {
                return null;
            }

            var top = _stack[_stack.Count - 1];
            return top != null ? top : null;
        }

        // --- 待機列（ベース表示の直列化） ---

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
