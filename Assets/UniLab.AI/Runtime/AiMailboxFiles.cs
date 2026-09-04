#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>同一ディレクトリ内の rename を境界として要求・応答を公開します。</summary>
    public static class AiMailboxFiles
    {
        private const int ResponseRetentionHours = 1;
        private const string RequestPrefix = "req-";
        private const string ResponsePrefix = "res-";
        private const string JsonExtension = ".json";

        /// <summary>完成済みの要求だけを名前順に列挙し、書きかけを除外します。</summary>
        public static string[] GetRequests(string directory)
        {
            var paths = System.IO.Directory.GetFiles(directory, "req-*.json");
            Array.Sort(paths, StringComparer.Ordinal);
            return paths;
        }

        /// <summary>要求用の正式ファイルだけを検証して復元します。</summary>
        public static AiCommandRequest ReadRequest(string path)
        {
            var name = Path.GetFileName(path);
            if (!name.StartsWith(RequestPrefix, StringComparison.Ordinal) || !name.EndsWith(JsonExtension, StringComparison.Ordinal))
            {
                throw new ArgumentException("正式な要求ファイルではありません。");
            }

            var json = File.ReadAllText(path);
            AiJsonObject.Parse(json);
            return JsonUtility.FromJson<AiCommandRequest>(json);
        }

        /// <summary>要求と同じ識別子の応答パスを返します。</summary>
        public static string GetResponsePath(string requestPath)
        {
            var name = Path.GetFileName(requestPath);
            return Path.Combine(Path.GetDirectoryName(requestPath), ResponsePrefix + name.Substring(RequestPrefix.Length));
        }

        /// <summary>一時ファイルを閉じてから移動し、既存の正式ファイルは上書きしません。</summary>
        public static void WriteAtomic(string path, string json)
        {
            var temporaryPath = path + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>応答を先に公開し、公開できた要求だけを削除します。</summary>
        public static void Complete(string requestPath, AiCommandResponse response)
        {
            var responsePath = GetResponsePath(requestPath);
            if (!File.Exists(responsePath))
            {
                WriteAtomic(responsePath, JsonUtility.ToJson(response, true));
            }

            File.Delete(requestPath);
        }

        /// <summary>起動時に保持期限を超えた応答だけを削除します。</summary>
        public static void CleanupResponses(string directory)
        {
            var oldest = DateTime.UtcNow.AddHours(-ResponseRetentionHours);
            foreach (var path in System.IO.Directory.GetFiles(directory, "res-*.json"))
            {
                if (File.GetLastWriteTimeUtc(path) < oldest)
                {
                    File.Delete(path);
                }
            }
        }
    }
}
#endif
