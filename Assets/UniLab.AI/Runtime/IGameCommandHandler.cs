#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace UniLab.AI
{
    /// <summary>
    /// ゲーム固有のコマンドを文字列名で公開するための入口です。
    /// AI ツール本体が利用側の DI や具体型を知らずに操作できるようにします。
    /// </summary>
    public interface IGameCommandHandler
    {
        /// <summary>
        /// AI が実行可能なコマンドを列挙できるようにします。
        /// 名前の公開が無いと呼び出し口を動的発見できないためです。
        /// </summary>
        IReadOnlyList<string> CommandNames { get; }

        /// <summary>
        /// 名前付きコマンドを実行します。
        /// 未知のコマンドを例外にせず false へ落とすことで汎用ライブラリ側の分岐を単純化します。
        /// </summary>
        bool TryExecute(string commandName, IReadOnlyDictionary<string, string> arguments, out string message);
    }
}
#endif
