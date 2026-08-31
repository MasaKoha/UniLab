using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace UniLab.MasterData
{
    /// <summary>
    /// UnityWebRequest で StreamingAssets を読む実装。
    /// Android では StreamingAssets が圧縮 APK の中にあり直接のファイル IO が使えないため、この経路を通す。
    /// </summary>
    public sealed class WebRequestStreamingAssetsReader : IStreamingAssetsReader
    {
        /// <inheritdoc/>
        public async UniTask<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        {
            using var request = UnityWebRequest.Get(path);
            await request.SendWebRequest().WithCancellation(cancellationToken);

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new IOException($"Failed to read local master: {path} => {request.error}");
            }

            return request.downloadHandler.data;
        }
    }
}
