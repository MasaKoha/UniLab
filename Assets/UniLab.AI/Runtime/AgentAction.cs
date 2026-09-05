#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 04 の入力語彙と submit を 1 手 JSON として受け取り、外部 LLM の出力形式を固定します。
    /// </summary>
    [Serializable]
    public sealed class AgentAction
    {
        /// <summary>UI の submit 経路を直接検証するための対象名です。</summary>
        public string submit;

        /// <summary>指定要素を祖先 ScrollRect の表示範囲へ入れる。フォーカスは動かさない。</summary>
        public string scrollTo;

        /// <summary>ゲームパッド単打を文字列 JSON で選べるようにするためのボタン名です。</summary>
        public string press;

        /// <summary>長押し操作を単打と分け、押下継続が意味を持つ UI を扱うためのボタン名です。</summary>
        public string hold;

        /// <summary>フォーカス移動を D-Pad 語彙へ寄せるための方向名です。</summary>
        public string move;

        /// <summary>左右スティックの意味を固定するための軸名です。</summary>
        public string stick;

        /// <summary>キーボード入力をパッド入力と同じ 1 手として扱うためのキー名です。</summary>
        public string key;

        /// <summary>TMP 入力欄へ OS 非依存に文字を流し込むための文字列です。</summary>
        public string text;

        /// <summary>hover やポインタ依存 UI をクリック前に解決するための対象名です。</summary>
        public string pointerMove;

        /// <summary>要素名クリックを座標クリックへ正規化するための対象名です。</summary>
        public string click;

        /// <summary>押下中の移動を通常クリックと区別するための語彙です。</summary>
        public string drag;

        /// <summary>スクロール量を対象位置と結びつけるための対象名です。</summary>
        public string scroll;

        /// <summary>タッチ UI をマウスクリックと分けて検証するための対象名です。</summary>
        public string tap;

        /// <summary>タッチの移動操作をマウスドラッグと分けるための語彙です。</summary>
        public string swipe;

        /// <summary>2 指操作の中心と距離を 1 手に閉じるための語彙です。</summary>
        public string pinch;

        /// <summary>ドラッグやスワイプの始点を要素名で表すための対象名です。</summary>
        public string from;

        /// <summary>ドラッグやスワイプの終点を要素名で表すための対象名です。</summary>
        public string to;

        /// <summary>ピンチ中心を要素名で表すための対象名です。</summary>
        public string center;

        /// <summary>左クリック以外を明示するためのポインタボタン名です。</summary>
        public string button;

        /// <summary>長押しや移動操作の継続時間を語彙間で共有するための秒数です。</summary>
        public float seconds;

        /// <summary>座標指定とスティック入力の X 成分を共有するための値です。</summary>
        public float x;

        /// <summary>座標指定とスティック入力の Y 成分を共有するための値です。</summary>
        public float y;

        /// <summary>始点座標を要素なしでも指定できるようにするための値です。</summary>
        public float fromX;

        /// <summary>始点座標を要素なしでも指定できるようにするための値です。</summary>
        public float fromY;

        /// <summary>終点座標を要素なしでも指定できるようにするための値です。</summary>
        public float toX;

        /// <summary>終点座標を要素なしでも指定できるようにするための値です。</summary>
        public float toY;

        /// <summary>スクロール量や距離のような単独スカラーを共有するための値です。</summary>
        public float amount;

        /// <summary>ピンチ開始距離を JSON から復元するための値です。</summary>
        public float fromDistance;

        /// <summary>ピンチ終了距離を JSON から復元するための値です。</summary>
        public float toDistance;

        /// <summary>外部 LLM の判断理由を UniLab.AI 側の行動ログへ残すための文字列です。</summary>
        public string reason;

        /// <summary>行動後の落ち着いた観測で検証する事後条件です。</summary>
        public ScenarioExpectation[] expect;
    }
}
#endif
