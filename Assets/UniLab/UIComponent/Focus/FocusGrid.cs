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

        /// <summary>
        /// このグリッド固有のラップモード。null なら FocusNavigator に渡した既定値に従う。
        /// 「基本は既定に任せ、必要な画面だけ上書きする」形にするため null 許容にしている。
        /// 可視化・デバッグ用途で外部から位相を読むためにも公開する。
        /// </summary>
        public FocusWrapMode? WrapModeOverride { get; }

        /// <summary>行数。可視化・デバッグ用途で外部から位相を読むために公開する。</summary>
        public int RowCount => _rows.Count;

        /// <summary>
        /// Selectable の行グリッドを受け取る。全 Selectable の navigation.mode を None にして
        /// Unity 標準の自動/明示ナビゲーションを完全に無効化する（方向解決は本クラスに一元化するため）。
        /// </summary>
        /// <param name="rows">行ごとの Selectable。</param>
        /// <param name="wrapModeOverride">このグリッドだけラップモードを変えたい場合に指定する。null なら FocusNavigator の既定値に従う。</param>
        public FocusGrid(IReadOnlyList<IReadOnlyList<Selectable>> rows, FocusWrapMode? wrapModeOverride = null)
        {
            _rows = rows;
            WrapModeOverride = wrapModeOverride;
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

            _gridModel = new FocusGridModel(_enabledFlagsByRow);
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
        public bool IsEnabled(FocusCell cell, bool includeNonInteractable)
        {
            RefreshEnabledFlags(includeNonInteractable);
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
        /// <param name="defaultWrapMode">FocusNavigator の既定ラップモード。<see cref="WrapModeOverride"/> が指定されていればそちらが優先される。</param>
        public bool TryResolve(
            FocusCell current,
            int desiredColumnIndex,
            FocusDirection direction,
            bool includeNonInteractable,
            FocusWrapMode defaultWrapMode,
            out FocusCell next)
        {
            RefreshEnabledFlags(includeNonInteractable);
            return _gridModel.TryResolve(current, desiredColumnIndex, direction, ResolveWrapMode(defaultWrapMode), out next);
        }

        /// <summary>このグリッドに実際に適用されるラップモードを返す。</summary>
        public FocusWrapMode ResolveWrapMode(FocusWrapMode defaultWrapMode)
        {
            return WrapModeOverride ?? defaultWrapMode;
        }

        /// <summary>セル位置に対応する Selectable を返す。</summary>
        public Selectable GetSelectable(FocusCell cell)
        {
            return _rows[cell.RowIndex][cell.ColumnIndex];
        }

        /// <summary>
        /// startRowIndex 行目以降で最初の有効セルに対応する Selectable を返す。見つからなければ false。
        /// タブバー行を飛ばして中身の先頭へフォーカスする用途で開始行を指定する。
        /// </summary>
        public bool TryGetFirstSelectable(bool includeNonInteractable, int startRowIndex, out Selectable selectable)
        {
            RefreshEnabledFlags(includeNonInteractable);

            if (_gridModel.TryGetFirstEnabledCell(startRowIndex, out var cell))
            {
                selectable = GetSelectable(cell);
                return true;
            }

            selectable = null;
            return false;
        }

        /// <summary>
        /// 全 Selectable の現在の有効状態を毎回再計算する。
        /// 表示更新のたびに結線をやり直す必要がなくなる代わりに、方向解決の直前に必ず呼ぶ。
        /// includeNonInteractable が true のときは interactable=false のセルもフォーカス対象に含める
        /// （押せないボタンでも枠が乗ることで、そこに項目があること自体は伝わるため）。
        /// 非アクティブ（SetActive(false)）なセルは見えていないので常に除外する。
        /// </summary>
        private void RefreshEnabledFlags(bool includeNonInteractable)
        {
            // perf: 行の要素数は固定なので既存配列を書き換えるだけにし、new を発生させない
            for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                var row = _rows[rowIndex];
                var flags = _enabledFlagsByRow[rowIndex];

                for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    var selectable = row[columnIndex];
                    flags[columnIndex] = selectable.gameObject.activeInHierarchy
                        && (includeNonInteractable || selectable.IsInteractable());
                }
            }
        }
    }
}
