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

        /// <summary>
        /// 画面遷移や演出で操作を受け付けられない間を AI ツールへ伝える口です。
        /// 未登録時はシーンロードと継続入力だけで落ち着きを判定します。null を許容します。
        /// </summary>
        public static IGameBusyProvider BusyProvider { get; set; }

        /// <summary>登録済みの busy 判定を安全に読みます。未登録・例外時は busy でない扱いです。</summary>
        public static bool IsGameBusy(out string reason)
        {
            reason = string.Empty;
            var provider = BusyProvider;
            if (provider == null)
            {
                return false;
            }

            try
            {
                if (!provider.IsBusy)
                {
                    return false;
                }

                reason = provider.Reason ?? string.Empty;
                return true;
            }
            catch (System.Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[GameAdapterRegistry] BusyProvider が例外を投げたため busy ではない扱いにします: {exception.Message}");
                return false;
            }
        }
    }
}
#endif
