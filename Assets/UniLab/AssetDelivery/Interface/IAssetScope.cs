using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.AssetDelivery
{
    /// <summary>
    /// Provides screen or scene scoped asset loading so disposing the scope releases every tracked handle through one lifetime boundary.
    /// </summary>
    public interface IAssetScope : IDisposable
    {
        /// <summary>
        /// Loads an asset by key for the owning screen or scene and tracks its handle until the scope is disposed.
        /// </summary>
        UniTask<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken);

        /// <summary>
        /// Instantiates a GameObject by key under the requested parent and tracks its handle until the scope is disposed.
        /// </summary>
        UniTask<GameObject> InstantiateAsync(string key, Transform parent, CancellationToken cancellationToken);
    }
}
