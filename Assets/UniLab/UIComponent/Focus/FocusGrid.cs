using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Focus
{
    /// <summary>
    /// Selectable の行グリッドと FocusGridModel を橋渡しする UnityEngine 依存クラス。
    /// 有効/無効の判定は結線時ではなく移動解決の都度計算するため、表示更新後に
    /// 結線し直すのを忘れるというバグのクラスが原理的に発生しない。
    /// </summary>
    public sealed class FocusGrid
    {
        private readonly IReadOnlyList<IReadOnlyList<Selectable>> _rows;

        // perf: 行の長さは不変なので、有効フラグ用の bool[] は生成時に確保して使い回す。
        // 移動のたびに new すると毎フレーム GC を発生させてしまう。
        private readonly bool[][] _enabledFlagsByRow;
        private readonly FocusGridModel _gridModel;

        /// <summary>ラップモード。可視化・デバッグ用途で外部から位相を読むために公開する。</summary>
        public FocusWrapMode WrapMode { get; }

        /// <summary>行数。可視化・デバッグ用途で外部から位相を読むために公開する。</summary>
        public int RowCount => _rows.Count;

        /// <summary>
        /// Selectable の行グリッドを受け取る。全 Selectable の navigation.mode を None にして
        /// Unity 標準の自動/明示ナビゲーションを完全に無効化する（方向解決は本クラスに一元化するため）。
        /// </summary>
        public FocusGrid(IReadOnlyList<IReadOnlyList<Selectable>> rows, FocusWrapMode wrapMode)
        {
            _rows = rows;
            WrapMode = wrapMode;
            _enabledFlagsByRow = new bool[rows.Count][];

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                _enabledFlagsByRow[rowIndex] = new bool[row.Count];

                for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    var navigation = row[columnIndex].navigation;
                    navigation.mode = Navigation.Mode.None;
                    row[columnIndex].navigation = navigation;
                }
            }

            _gridModel = new FocusGridModel(_enabledFlagsByRow, wrapMode);
        }

        /// <summary>指定行の列数を返す。可視化・デバッグ用途で外部から位相を読むために公開する。</summary>
        public int GetColumnCount(int rowIndex)
        {
            return _rows[rowIndex].Count;
        }

        /// <summary>
        /// 指定セルが現時点で選択可能かを返す。呼び出しのたびに再計算する。
        /// 可視化・デバッグ用途で外部から位相を読むために公開する。
        /// </summary>
        public bool IsEnabled(FocusCell cell)
        {
            RefreshEnabledFlags();
            return _gridModel.IsEnabled(cell);
        }

        /// <summary>指定の GameObject を保持している Selectable のセル位置を探す。見つからなければ false。</summary>
        public bool TryFindCell(GameObject target, out FocusCell cell)
        {
            for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                var row = _rows[rowIndex];
                for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    if (row[columnIndex].gameObject == target)
                    {
                        cell = new FocusCell(rowIndex, columnIndex);
                        return true;
                    }
                }
            }

            cell = FocusCell.Invalid;
            return false;
        }

        /// <summary>current から direction 方向へ移動した先の有効セルを解決する。</summary>
        public bool TryResolve(FocusCell current, int desiredColumnIndex, FocusDirection direction, out FocusCell next)
        {
            RefreshEnabledFlags();
            return _gridModel.TryResolve(current, desiredColumnIndex, direction, out next);
        }

        /// <summary>セル位置に対応する Selectable を返す。</summary>
        public Selectable GetSelectable(FocusCell cell)
        {
            return _rows[cell.RowIndex][cell.ColumnIndex];
        }

        /// <summary>先頭の有効セルに対応する Selectable を返す。見つからなければ false。</summary>
        public bool TryGetFirstSelectable(out Selectable selectable)
        {
            RefreshEnabledFlags();

            if (_gridModel.TryGetFirstEnabledCell(out var cell))
            {
                selectable = GetSelectable(cell);
                return true;
            }

            selectable = null;
            return false;
        }

        /// <summary>
        /// 全 Selectable の現在の有効状態（アクティブかつ操作可能か）を毎回再計算する。
        /// 表示更新のたびに結線をやり直す必要がなくなる代わりに、方向解決の直前に必ず呼ぶ。
        /// </summary>
        private void RefreshEnabledFlags()
        {
            // perf: 行の要素数は固定なので既存配列を書き換えるだけにし、new を発生させない
            for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                var row = _rows[rowIndex];
                var flags = _enabledFlagsByRow[rowIndex];

                for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    var selectable = row[columnIndex];
                    flags[columnIndex] = selectable.gameObject.activeInHierarchy && selectable.IsInteractable();
                }
            }
        }
    }
}
