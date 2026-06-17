using System.Collections.Generic;
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

        /// <summary>
        /// keys のアセットを型 <typeparamref name="T"/> として事前ロードし、cache に保持（pin）します。
        /// ロード待ちを使用前（ローディング画面・シーン遷移）に前倒しし、以降の <see cref="AcquireAsync{T}"/> を即時化します。
        /// pin した分は <see cref="ReleasePrewarm"/> を呼ぶまで TTL/LRU で破棄されません。メモリ予算のため範囲を区切って使ってください。
        /// </summary>
        UniTask PrewarmAsync<T>(IReadOnlyList<string> keys, CancellationToken cancellationToken);

        /// <summary>
        /// <see cref="PrewarmAsync{T}"/> で pin した保持をすべて解放します（参照カウントを手放し、以降は TTL/LRU 管理に戻します）。
        /// </summary>
        void ReleasePrewarm();

        /// <summary>
        /// cache の現在の占有状況（エントリ数・参照中・pin 中・参照合計）のスナップショットを取得します。ランタイム診断用です。
        /// </summary>
        AssetVaultCacheStats GetStats();
    }
}
