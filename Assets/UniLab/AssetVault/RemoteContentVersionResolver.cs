using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UniLab.AssetVault
{
    /// <summary>
    /// CDN 上の version.json から現在のコンテンツ版を解決する HTTP 実装です。
    /// </summary>
    public sealed class RemoteContentVersionResolver : IContentVersionResolver
    {
        private readonly string _url;
        private readonly Func<string, CancellationToken, UniTask<string>> _fetchAsync;

        /// <summary>
        /// 解決済みの基底 URL から version.json の URL を作成します。
        /// requestTimeoutSeconds は起動必須の取得が無応答の CDN で無限に待たないための上限です。
        /// </summary>
        public RemoteContentVersionResolver(string baseUrl, int requestTimeoutSeconds = 15)
            : this(baseUrl, CreateUnityWebRequestFetcher(requestTimeoutSeconds))
        {
        }

        /// <summary>
        /// 解決済みの基底 URL と version.json 取得処理から resolver を作成します。
        /// </summary>
        public RemoteContentVersionResolver(string baseUrl, Func<string, CancellationToken, UniTask<string>> fetchAsync)
        {
            _url = $"{baseUrl.TrimEnd('/')}/version.json";
            _fetchAsync = fetchAsync;
        }

        /// <summary>
        /// version.json を取得し、RemoteLoadPath に使うコンテンツ版情報を返します。
        /// </summary>
        public async UniTask<ContentVersionInfo> ResolveAsync(CancellationToken cancellationToken)
        {
            try
            {
                var json = await _fetchAsync(_url, cancellationToken);
                var contentVersionJson = JsonUtility.FromJson<ContentVersionJson>(json);
                if (contentVersionJson == null)
                {
                    throw new AssetVaultException($"Failed to resolve content version from {_url}. Invalid version json.");
                }

                return new ContentVersionInfo(contentVersionJson.contentVersion, contentVersionJson.path);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw AssetVaultOperationGuard.ToAssetVaultException(exception, $"Failed to resolve content version from {_url}.");
            }
        }

        private static Func<string, CancellationToken, UniTask<string>> CreateUnityWebRequestFetcher(int requestTimeoutSeconds)
        {
            return async (url, cancellationToken) =>
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = requestTimeoutSeconds;
                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        throw new AssetVaultException($"Failed to resolve content version from {url}. Error: {request.error}");
                    }

                    return request.downloadHandler.text;
                }
            };
        }

        [Serializable]
        private class ContentVersionJson
        {
            /// <summary>
            /// 文字列一致で版変更を判定する内部版 ID です。
            /// </summary>
            public string contentVersion;

            /// <summary>
            /// RemoteLoadPath の ContentPath に使う公開 URL の不透明セグメントです。
            /// </summary>
            public string path;
        }
    }
}
