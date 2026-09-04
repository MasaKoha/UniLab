#if UNILAB_AI_PIPELINE
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniLab.AI.Pipeline
{
    /// <summary>
    /// 現在の UI スナップショットを Unity 公式 CLI へ公開します。
    /// 既存の `UiSnapshot` をそのまま叩けるようにし、`eval` 文字列生成を不要にします。
    /// </summary>
    public static class AiSnapshotCliCommand
    {
        /// <summary>
        /// UI スナップショットを取得し、必要なら保存します。
        /// </summary>
        [CliCommand("ai_snapshot", "現在の UI スナップショットを取得します。", Tags = new[] { "ui" })]
        public static object Capture(
            [CliArg("compact", "圧縮テキストを返すか。")] bool compact = true,
            [CliArg("save", "DebugOutput/snapshots へ保存するか。")] bool save = false)
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest
            {
                op = "snapshot",
                args = JsonUtility.ToJson(new AiCliArguments { compact = compact, save = save }),
            });
            return JsonUtility.ToJson(response, true);
        }
    }
}
#endif
