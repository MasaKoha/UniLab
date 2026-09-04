using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.UI.Focus
{
    /// <summary>
    /// IFocusNavigator の実装。EventSystem と寿命を揃えるためシーンごとに生成し、フォーカスグリッドの
    /// スタックと方向入力に応じた選択切り替えを一元管理する。EventSystem は選択状態の保持のみ担当させる。
    /// </summary>
    public sealed class FocusNavigator : MonoBehaviour, IFocusNavigator, IDisposable
    {
        private readonly CompositeDisposable _disposables = new();
        private readonly List<FocusGrid> _gridStack = new();
        private int _desiredColumnIndex;

        /// <summary>押せない項目にもフォーカスを乗せるか。Initialize で受け取り、全ての方向解決へ引き渡す。</summary>
        private bool _focusNonInteractable;
        private FocusWrapMode _defaultWrapMode;

        // EventSystem はシーンごとに生成・破棄されるため、シーンをまたいで参照を持ち越さないよう
        // 初期化時にそのシーンのものを受け取る
        private EventSystem _eventSystem;

        /// <summary>積まれているグリッドを下から順に返す。可視化・デバッグ用途で外部から位相を読むために公開する。</summary>
        public IReadOnlyList<FocusGrid> GridStack => _gridStack;

        /// <summary>スタック最上位のアクティブグリッド。空なら null。可視化・デバッグ用途で外部から位相を読むために公開する。</summary>
        public FocusGrid ActiveGrid => _gridStack.Count == 0 ? null : _gridStack[_gridStack.Count - 1];

        /// <inheritdoc/>
        public Selectable CurrentSelected
        {
            get
            {
                // Initialize 前や、選択が外れている（画面遷移直後など）ケースがあるため素直に null を返す
                var selectedObject = _eventSystem == null ? null : _eventSystem.currentSelectedGameObject;
                return selectedObject == null ? null : selectedObject.GetComponent<Selectable>();
            }
        }

        /// <summary>上下移動で維持している列記憶。可視化・デバッグ用途で外部から位相を読むために公開する。</summary>
        /// <inheritdoc/>
        public bool FocusNonInteractable => _focusNonInteractable;

        /// <inheritdoc/>
        public FocusWrapMode DefaultWrapMode => _defaultWrapMode;

        /// <summary>上下移動で維持している列記憶。</summary>
        public int DesiredColumnIndex => _desiredColumnIndex;

        /// <summary>
        /// 方向入力ストリームと操作対象の EventSystem を受け取り、方向解決を開始する。
        /// このシーンの Presenter が初期化時に一度だけ呼ぶ。
        /// 入力元の具体的な型を知らないことで、UniLab から利用側の入力実装へ依存しないようにする。
        /// </summary>
        public void Initialize(Observable<FocusDirection> moveStream, EventSystem eventSystem, bool focusNonInteractable, FocusWrapMode defaultWrapMode)
        {
            _focusNonInteractable = focusNonInteractable;
            _defaultWrapMode = defaultWrapMode;
            _eventSystem = eventSystem;
            moveStream.Subscribe(HandleMove).AddTo(_disposables);
        }

        /// <inheritdoc/>
        public void PushGrid(FocusGrid grid)
        {
            _gridStack.Add(grid);
        }

        /// <inheritdoc/>
        public void PopGrid(FocusGrid grid)
        {
            // 最上位一致のときだけ降ろす方式だと、他のグリッドが上に積まれている間に再構築が走った場合に
            // 古いグリッドが降ろされず埋もれて積み残る（実機でスタックが6枚まで育った）。
            // 所有者が「自分のグリッドを降ろす」意図は位置に依らないため、どこにあっても取り除く。
            var index = _gridStack.LastIndexOf(grid);
            if (index < 0)
            {
                return;
            }

            _gridStack.RemoveAt(index);
        }

        /// <inheritdoc/>
        public void SetSelected(Selectable selectable)
        {
            // Initialize より前に呼ばれた場合など、EventSystem 未設定のケースを想定して null になり得る
            if (_eventSystem == null)
            {
                return;
            }

            // 選択処理の途中（OnSelect の連鎖の中）から呼ばれると EventSystem が再入を拒否して
            // 「Attempting to select ... while already selecting an object」を出す。
            // 同じ対象を選び直す必要はなく、別対象でも EventSystem 側が受け付けないため、ここで打ち切る
            if (_eventSystem.alreadySelecting || _eventSystem.currentSelectedGameObject == selectable.gameObject)
            {
                return;
            }

            _eventSystem.SetSelectedGameObject(selectable.gameObject);

            if (_gridStack.Count > 0 && _gridStack[_gridStack.Count - 1].TryFindCell(selectable.gameObject, out var cell))
            {
                _desiredColumnIndex = cell.ColumnIndex;
            }
        }

        /// <inheritdoc/>
        public void FocusFirst(int startRowIndex)
        {
            if (_gridStack.Count == 0)
            {
                return;
            }

            if (_gridStack[_gridStack.Count - 1].TryGetFirstSelectable(_focusNonInteractable, startRowIndex, out var selectable))
            {
                SetSelected(selectable);
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>方向入力の購読をすべて破棄する。</summary>
        public void Dispose()
        {
            _disposables.Dispose();
        }

        /// <summary>
        /// 方向入力を受けてアクティブグリッド（スタック最上位）上で選択を1セル分移動する。
        /// </summary>
        private void HandleMove(FocusDirection direction)
        {
            if (_gridStack.Count == 0 || _eventSystem == null)
            {
                return;
            }

            var activeGrid = _gridStack[_gridStack.Count - 1];

            // ダイアログなどグリッド外にフォーカスがある間は、背後のグリッドを勝手に動かさない
            if (!activeGrid.TryFindCell(_eventSystem.currentSelectedGameObject, out var currentCell))
            {
                return;
            }

            if (!activeGrid.TryResolve(currentCell, _desiredColumnIndex, direction, _focusNonInteractable, _defaultWrapMode, out var next))
            {
                return;
            }

            _eventSystem.SetSelectedGameObject(activeGrid.GetSelectable(next).gameObject);

            // 上下移動では列記憶を維持し、次に左右移動したときに近い列へ戻れるようにする
            if (direction == FocusDirection.Left || direction == FocusDirection.Right)
            {
                _desiredColumnIndex = next.ColumnIndex;
            }
        }
    }
}
