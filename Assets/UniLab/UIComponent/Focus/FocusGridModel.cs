using System;
using System.Collections.Generic;

namespace UniLab.UI.Focus
{
    /// <summary>
    /// フォーカス位相（行の並びと各セルの有効/無効）を保持し、方向解決だけを行う
    /// UnityEngine 非依存の純粋クラス。EditMode テストの対象本体。
    /// </summary>
    public sealed class FocusGridModel
    {
        private readonly IReadOnlyList<IReadOnlyList<bool>> _rows;
        private readonly FocusWrapMode _wrapMode;

        /// <summary>行ごとのセル有効フラグ列とラップモードを受け取る。行の長さは可変長でよい。</summary>
        public FocusGridModel(IReadOnlyList<IReadOnlyList<bool>> rows, FocusWrapMode wrapMode)
        {
            _rows = rows;
            _wrapMode = wrapMode;
        }

        /// <summary>行数。</summary>
        public int RowCount => _rows.Count;

        /// <summary>指定行の列数を返す。</summary>
        public int GetColumnCount(int rowIndex)
        {
            return _rows[rowIndex].Count;
        }

        /// <summary>指定セルが選択可能かを返す。範囲外なら false。</summary>
        public bool IsEnabled(FocusCell cell)
        {
            if (!IsCellInRange(cell))
            {
                return false;
            }

            return _rows[cell.RowIndex][cell.ColumnIndex];
        }

        /// <summary>
        /// startRowIndex 行目から順に走査し、最初に見つかった有効セルを返す。
        /// タブバーのような共通行を先頭に持つグリッドで、その行を飛ばして
        /// 中身の先頭へフォーカスするために開始行を指定できるようにしている。
        /// </summary>
        public bool TryGetFirstEnabledCell(int startRowIndex, out FocusCell cell)
        {
            for (var rowIndex = startRowIndex; rowIndex < RowCount; rowIndex++)
            {
                var columnCount = GetColumnCount(rowIndex);
                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    var candidate = new FocusCell(rowIndex, columnIndex);
                    if (IsEnabled(candidate))
                    {
                        cell = candidate;
                        return true;
                    }
                }
            }

            cell = FocusCell.Invalid;
            return false;
        }

        /// <summary>
        /// current から direction の方向へ移動した先の有効セルを解決する。
        /// 左右は同一行内の列走査、上下は desiredColumnIndex に最も近い列を持つ行を探す。
        /// </summary>
        public bool TryResolve(FocusCell current, int desiredColumnIndex, FocusDirection direction, out FocusCell next)
        {
            next = FocusCell.Invalid;

            if (direction == FocusDirection.None || !IsCellInRange(current))
            {
                return false;
            }

            switch (direction)
            {
                case FocusDirection.Left:
                    return TryResolveHorizontal(current, -1, out next);
                case FocusDirection.Right:
                    return TryResolveHorizontal(current, 1, out next);
                case FocusDirection.Up:
                    return TryResolveVertical(current, desiredColumnIndex, -1, out next);
                case FocusDirection.Down:
                    return TryResolveVertical(current, desiredColumnIndex, 1, out next);
                default:
                    return false;
            }
        }

        /// <summary>行内で columnStep 方向へ列を走査し、最初に見つかった有効セルを返す。</summary>
        private bool TryResolveHorizontal(FocusCell current, int columnStep, out FocusCell next)
        {
            next = FocusCell.Invalid;

            var columnCount = GetColumnCount(current.RowIndex);
            var wrapAllowed = _wrapMode == FocusWrapMode.Horizontal || _wrapMode == FocusWrapMode.Both;
            var columnIndex = current.ColumnIndex;

            // columnCount - 1 回のループで他の全列をちょうど1周分だけ走査できる（自分自身には戻らない）
            for (var step = 0; step < columnCount - 1; step++)
            {
                columnIndex += columnStep;
                if (columnIndex < 0 || columnIndex >= columnCount)
                {
                    if (!wrapAllowed)
                    {
                        return false;
                    }

                    columnIndex = ((columnIndex % columnCount) + columnCount) % columnCount;
                }

                var candidate = new FocusCell(current.RowIndex, columnIndex);
                if (IsEnabled(candidate))
                {
                    next = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>rowStep 方向へ行を走査し、有効セルを持つ最初の行から desiredColumnIndex に最も近い列を選ぶ。</summary>
        private bool TryResolveVertical(FocusCell current, int desiredColumnIndex, int rowStep, out FocusCell next)
        {
            next = FocusCell.Invalid;

            var wrapAllowed = _wrapMode == FocusWrapMode.Vertical || _wrapMode == FocusWrapMode.Both;
            var rowIndex = current.RowIndex;

            // RowCount - 1 回のループで他の全行をちょうど1周分だけ走査できる（自分自身には戻らない）
            for (var step = 0; step < RowCount - 1; step++)
            {
                rowIndex += rowStep;
                if (rowIndex < 0 || rowIndex >= RowCount)
                {
                    if (!wrapAllowed)
                    {
                        return false;
                    }

                    rowIndex = ((rowIndex % RowCount) + RowCount) % RowCount;
                }

                if (TryGetClosestEnabledColumn(rowIndex, desiredColumnIndex, out var columnIndex))
                {
                    next = new FocusCell(rowIndex, columnIndex);
                    return true;
                }
            }

            return false;
        }

        /// <summary>指定行の中から desiredColumnIndex に最も近い有効セルの列を探す。同距離なら小さい列を優先する。</summary>
        private bool TryGetClosestEnabledColumn(int rowIndex, int desiredColumnIndex, out int columnIndex)
        {
            columnIndex = -1;
            var bestDistance = int.MaxValue;
            var columnCount = GetColumnCount(rowIndex);

            for (var candidateColumnIndex = 0; candidateColumnIndex < columnCount; candidateColumnIndex++)
            {
                if (!IsEnabled(new FocusCell(rowIndex, candidateColumnIndex)))
                {
                    continue;
                }

                var distance = Math.Abs(candidateColumnIndex - desiredColumnIndex);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    columnIndex = candidateColumnIndex;
                }
            }

            return columnIndex >= 0;
        }

        /// <summary>セルの行・列インデックスがともに現在の位相の範囲内かを判定する。</summary>
        private bool IsCellInRange(FocusCell cell)
        {
            if (cell.RowIndex < 0 || cell.RowIndex >= RowCount)
            {
                return false;
            }

            var columnCount = GetColumnCount(cell.RowIndex);
            return cell.ColumnIndex >= 0 && cell.ColumnIndex < columnCount;
        }
    }
}
