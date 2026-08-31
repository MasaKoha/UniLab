using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.MasterData
{
    /// <summary>
    /// 通常のファイル IO で StreamingAssets を読む実装。Android 以外のプラットフォームで使う。
    /// </summary>
    public sealed class FileStreamingAssetsReader : IStreamingAssetsReader
    {
        /// <inheritdoc/>
        public async UniTask<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
    }
}
