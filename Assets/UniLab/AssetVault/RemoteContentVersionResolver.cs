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
        private readonly int _requestTimeoutSeconds;

        /// <summary>
        /// コンテンツ配信の基底 URL と環境名から version.json の URL を作成します。
        /// requestTimeoutSeconds は起動必須の取得が無応答の CDN で無限に待たないための上限です。
        /// </summary>
        public RemoteContentVersionResolver(string contentBaseUrl, string environment, int requestTimeoutSeconds = 15)
        {
            var normalizedContentBaseUrl = contentBaseUrl.TrimEnd('/');
            _url = $"{normalizedContentBaseUrl}/{environment}/version.json";
            _requestTimeoutSeconds = requestTimeoutSeconds;
        }

        /// <summary>
        /// version.json を取得し、RemoteLoadPath に使うコンテンツ版情報を返します。
        /// </summary>
        public async UniTask<ContentVersionInfo> ResolveAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var request = UnityWebRequest.Get(_url))
                {
                    request.timeout = _requestTimeoutSeconds;
                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        throw new AssetVaultException($"Failed to resolve content version from {_url}. Error: {request.error}");
                    }

                    var dto = JsonUtility.FromJson<ContentVersionJson>(request.downloadHandler.text);
                    return new ContentVersionInfo(dto.contentVersion, dto.path);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw AssetVaultOperationGuard.ToAssetVaultException(exception, $"Failed to resolve content version from {_url}.");
            }
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
