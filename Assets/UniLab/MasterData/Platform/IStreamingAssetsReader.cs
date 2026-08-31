using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.MasterData
{
    /// <summary>
    /// StreamingAssets からバイナリを読む手段。プラットフォームで読み方が変わるため抽象化する。
    /// Android では StreamingAssets が圧縮 APK の中にあり直接のファイル IO が使えない。
    /// </summary>
    public interface IStreamingAssetsReader
    {
        /// <summary>
        /// 指定パスのバイナリを読む。存在しない場合は null を返す。
        /// 読めるはずのものが読めなかった場合は例外を投げる。
        /// </summary>
        UniTask<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
    }
}
