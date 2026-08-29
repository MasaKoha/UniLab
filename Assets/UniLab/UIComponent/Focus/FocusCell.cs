using System;

namespace UniLab.UI.Focus
{
    /// <summary>
    /// フォーカスグリッド上の1セル位置（行・列インデックス）を表す不変値。
    /// </summary>
    public readonly struct FocusCell : IEquatable<FocusCell>
    {
        /// <summary>無効セルを表す定数（行・列とも -1）。移動解決に失敗した際の戻り値として使う。</summary>
        public static readonly FocusCell Invalid = new(-1, -1);

        /// <summary>行インデックス。</summary>
        public int RowIndex { get; }

        /// <summary>列インデックス。</summary>
        public int ColumnIndex { get; }

        /// <summary>行・列インデックスを指定してセルを生成する。</summary>
        public FocusCell(int rowIndex, int columnIndex)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }

        /// <summary>行・列インデックスがともに 0 以上であれば有効なセルとみなす。</summary>
        public bool IsValid => RowIndex >= 0 && ColumnIndex >= 0;

        /// <inheritdoc/>
        public bool Equals(FocusCell other)
        {
            return RowIndex == other.RowIndex && ColumnIndex == other.ColumnIndex;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is FocusCell other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(RowIndex, ColumnIndex);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"FocusCell({RowIndex}, {ColumnIndex})";
        }

        /// <summary>2つのセルが同じ位置を指すかを判定する。</summary>
        public static bool operator ==(FocusCell left, FocusCell right)
        {
            return left.Equals(right);
        }

        /// <summary>2つのセルが異なる位置を指すかを判定する。</summary>
        public static bool operator !=(FocusCell left, FocusCell right)
        {
            return !left.Equals(right);
        }
    }
}
