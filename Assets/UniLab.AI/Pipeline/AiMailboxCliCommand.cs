#if UNILAB_AI_PIPELINE
using System;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniLab.AI.Pipeline
{
    /// <summary>Unity 内蔵メールボックスの寿命だけを CLI から操作します。</summary>
    public static class AiMailboxCliCommand
    {
        /// <summary>開始・停止・状態照会のいずれか一つを処理します。</summary>
        [CliCommand("ai_mailbox", "メールボックスを管理します。", Tags = new[] { "ai" })]
        public static string Mailbox(
            [CliArg("start", "開始します。")] bool start = false,
            [CliArg("stop", "停止します。")] bool stop = false,
            [CliArg("status", "状態を返します。")] bool status = false,
            [CliArg("directory", "メールボックスの場所。")] string directory = "")
        {
            var response = new AiCommandResponse { op = "mailbox" };
            try
            {
                if ((start ? 1 : 0) + (stop ? 1 : 0) + (status ? 1 : 0) != 1)
                {
                    throw new ArgumentException("--start / --stop / --status の一つを指定してください。");
                }

                if (start)
                {
                    AiMailboxServer.Start(directory);
                }

                if (stop)
                {
                    AiMailboxServer.Stop();
                }

                response.ok = true;
                response.path = AiMailboxServer.Directory;
                response.text = $"running={AiMailboxServer.IsRunning} handledCount={AiMailboxServer.HandledCount}";
            }
            catch (Exception exception)
            {
                response.error = exception.Message;
            }

            return JsonUtility.ToJson(response, true);
        }
    }
}
#endif
