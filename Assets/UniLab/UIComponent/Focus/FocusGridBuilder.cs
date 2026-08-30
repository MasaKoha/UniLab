using System.Collections.Generic;
using UnityEngine.UI;

namespace UniLab.UI.Focus
{
    /// <summary>
    /// View から FocusGrid を読みやすく組み立てるためのビルダー。
    /// </summary>
    public sealed class FocusGridBuilder
    {
        private readonly List<IReadOnlyList<Selectable>> _rows = new();
        /// <summary>null のままなら FocusNavigator に渡した既定値に従う。</summary>
        private FocusWrapMode? _wrapModeOverride;

        /// <summary>Selectable の可変長引数で1行を追加する。要素0の行は無視する。</summary>
        public FocusGridBuilder AddRow(params Selectable[] selectables)
        {
            return AddRow((IReadOnlyList<Selectable>)selectables);
        }

        /// <summary>Selectable のリストで1行を追加する。要素0の行は無視する。</summary>
        public FocusGridBuilder AddRow(IReadOnlyList<Selectable> selectables)
        {
            if (selectables.Count == 0)
            {
                return this;
            }

            _rows.Add(selectables);
            return this;
        }

        /// <summary>
        /// このグリッドだけラップモードを上書きする。呼ばなければ FocusNavigator の既定値に従う。
        /// </summary>
        public FocusGridBuilder SetWrapMode(FocusWrapMode wrapMode)
        {
            _wrapModeOverride = wrapMode;
            return this;
        }

        /// <summary>これまでに追加した行から FocusGrid を生成する。</summary>
        public FocusGrid Build()
        {
            return new FocusGrid(_rows, _wrapModeOverride);
        }
    }
}
