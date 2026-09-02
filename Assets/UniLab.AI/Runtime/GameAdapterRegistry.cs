#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// ゲーム固有アダプタの登録先です。
    /// UniLab.AI が外部 DI へ依存しないまま利用側から実装を差し込めるように静的に保持します。
    /// </summary>
    public static class GameAdapterRegistry
    {
        /// <summary>
        /// スナップショットへゲーム状態を同梱したいときの読み出し口です。
        /// 未登録時は黙って省略する設計のため null を許容します。
        /// </summary>
        public static IGameStateProvider StateProvider { get; set; }

        /// <summary>
        /// ゲーム固有コマンドを AI ツールから実行したいときの入口です。
        /// 未登録時は機能を省く方針のため null を許容します。
        /// </summary>
        public static IGameCommandHandler CommandHandler { get; set; }
    }
}
#endif
