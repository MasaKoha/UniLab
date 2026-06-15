using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.AssetVault
{
    /// <summary>
    /// scope の破棄で追跡中の全 handle を 1 つの lifetime 境界で解放できる、画面または scene 単位の asset loading を提供します。
    /// </summary>
    public interface IAssetScope : IDisposable
    {
        /// <summary>
        /// 所有元の画面または scene 向けに key で asset をロードし、scope が破棄されるまで handle を追跡します。
        /// </summary>
        UniTask<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken);

        /// <summary>
        /// label を付与した asset 群を一括ロードし、scope が破棄されるまで handle を追跡します。
        /// 型 <typeparamref name="T"/> が結果のフィルタとして働くため、同一ラベル内に他の型が混在していても問題ありません。
        /// </summary>
        UniTask<IReadOnlyList<T>> LoadAssetsAsync<T>(string label, CancellationToken cancellationToken);

        /// <summary>
        /// 指定された parent 配下に key で GameObject を生成し、scope が破棄されるまで handle を追跡します。
        /// </summary>
        UniTask<GameObject> InstantiateAsync(string key, Transform parent, CancellationToken cancellationToken);
    }
}
