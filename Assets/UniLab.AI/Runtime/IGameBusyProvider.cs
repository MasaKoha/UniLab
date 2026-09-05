#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// ゲーム側が「今は操作を受け付けられない」状態（画面遷移のフェード・ローディング・演出中の入力ブロック）を
    /// AI ツールへ伝えるための入口です。落ち着き待ちがシーン単位の判定だけでは拾えない状態を補います。
    /// </summary>
    public interface IGameBusyProvider
    {
        /// <summary>操作を受け付けられない状態なら true。</summary>
        bool IsBusy { get; }

        /// <summary>busy の理由（例: "loading" / "transition" / "inputBlocked"）。観測テキストにそのまま載せます。</summary>
        string Reason { get; }
    }
}
#endif
