#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// シナリオの期待値を観測結果だけで判定し、自己申告の成功を混ぜないための JSON モデルです。
    /// </summary>
    [Serializable]
    public sealed class ScenarioExpectation
    {
        /// <summary>
        /// 判定器を文字列で選ばせ、JSON シナリオを専用型へ分けず拡張できるようにします。
        /// </summary>
        public string kind;

        /// <summary>
        /// テキスト、シーン名、性能しきい値を同じ欄で受け、単純な JSON を維持します。
        /// </summary>
        public string value;

        /// <summary>
        /// 要素パスを値と分け、文字列期待値との混同を避けるために保持します。
        /// </summary>
        public string target;

        /// <summary>
        /// テキスト探索の範囲を狭め、同じ文言が別パネルにある画面でも誤判定を避けます。
        /// </summary>
        public string scope;

        /// <summary>
        /// ゲーム固有状態の比較方法を JSON 側で明示し、登録がない場合は静かに省けるようにします。
        /// </summary>
        public string key;

        /// <summary>
        /// 数値比較と文字列比較のどちらも同じ期待値で扱うための比較演算子です。
        /// </summary>
        public string op;
    }
}
#endif
