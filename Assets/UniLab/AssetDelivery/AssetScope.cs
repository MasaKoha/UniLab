using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniLab.AssetDelivery
{
    internal sealed class AssetScope : IAssetScope
    {
        private readonly List<AsyncOperationHandle> _handles = new();

        /// <summary>
        /// Loads an asset by key and keeps its Addressables handle owned by this scope.
        /// </summary>
        public async UniTask<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken)
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<T>(key);
                _handles.Add(handle);

                var asset = await handle.ToUniTask(cancellationToken: cancellationToken);
                ThrowIfFailed(handle, $"Failed to load asset by key '{key}'.");
                return asset;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw ToAssetDeliveryException(exception, $"Failed to load asset by key '{key}'.");
            }
        }

        /// <summary>
        /// Instantiates a GameObject by key and keeps its Addressables handle owned by this scope.
        /// </summary>
        public async UniTask<GameObject> InstantiateAsync(string key, Transform parent, CancellationToken cancellationToken)
        {
            try
            {
                var handle = Addressables.InstantiateAsync(key, parent);
                _handles.Add(handle);

                var gameObject = await handle.ToUniTask(cancellationToken: cancellationToken);
                ThrowIfFailed(handle, $"Failed to instantiate asset by key '{key}'.");
                return gameObject;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw ToAssetDeliveryException(exception, $"Failed to instantiate asset by key '{key}'.");
            }
        }

        /// <summary>
        /// Releases every handle loaded through this scope so screen lifetime owns asset lifetime.
        /// </summary>
        public void Dispose()
        {
            foreach (var handle in _handles)
            {
                Addressables.Release(handle);
            }

            _handles.Clear();
        }

        private static void ThrowIfFailed(AsyncOperationHandle handle, string message)
        {
            if (handle.Status != AsyncOperationStatus.Failed)
            {
                return;
            }

            var exception = handle.OperationException ?? new InvalidOperationException(message);
            throw new AssetDeliveryException(message, exception);
        }

        private static AssetDeliveryException ToAssetDeliveryException(Exception exception, string message)
        {
            if (exception is AssetDeliveryException assetDeliveryException)
            {
                return assetDeliveryException;
            }

            return new AssetDeliveryException(message, exception);
        }
    }
}
