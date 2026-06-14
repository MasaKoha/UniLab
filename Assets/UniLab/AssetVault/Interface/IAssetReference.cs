using System;

namespace UniLab.AssetVault
{
    /// <summary>
    /// キャッシュから取得したアセットへの参照です。Dispose で参照カウントを1つ手放します
    /// （0 になっても TTL/LRU の猶予で即解放されるとは限りません）。
    /// </summary>
    public interface IAssetReference<out T> : IDisposable
    {
        /// <summary>解決済みのアセット本体です。</summary>
        T Value { get; }
    }
}
