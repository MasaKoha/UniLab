using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.AssetVault
{
    /// <summary>
    /// アセットを参照カウントで共有・キャッシュするための API です。
    /// 同じ key の Acquire は同一アセットを共有し、参照が 0 になっても TTL（猶予時間）/ LRU（件数上限）に従って遅延解放します。
    /// オブジェクトプールや「再利用ごとに差し替わるアセット」での再読み込み churn を抑えるために使います。
    /// </summary>
    public interface IAssetVaultCache
    {
        /// <summary>
        /// key で asset を取得します（未ロードならロード、既存なら共有して参照カウント+1）。
        /// 返り値の <see cref="IAssetReference{T}"/> を Dispose すると参照カウントを手放します。
        /// </summary>
        UniTask<IAssetReference<T>> AcquireAsync<T>(string key, CancellationToken cancellationToken);

        /// <summary>
        /// 期限切れ（TTL 超過）・上限超過（LRU）の未参照エントリを今すぐ解放します。任意タイミングで呼べます。
        /// </summary>
        void Trim();
    }
}
