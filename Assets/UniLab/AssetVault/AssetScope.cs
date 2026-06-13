using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniLab.AssetVault
{
    internal sealed class AssetScope : IAssetScope
    {
        private readonly List<AsyncOperationHandle> _handles = new();

        /// <summary>
        /// key で asset をロードし、その Addressables handle をこの scope の所有にします。
        /// </summary>
        public async UniTask<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken)
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<T>(key);
                _handles.Add(handle);

                var asset = await handle.ToUniTask(cancellationToken: cancellationToken);
                AssetVaultOperationGuard.ThrowIfFailed(handle, $"Failed to load asset by key '{key}'.");
                return asset;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw AssetVaultOperationGuard.ToAssetVaultException(exception, $"Failed to load asset by key '{key}'.");
            }
        }

        /// <summary>
        /// key で GameObject を生成し、その Addressables handle をこの scope の所有にします。
        /// </summary>
        public async UniTask<GameObject> InstantiateAsync(string key, Transform parent, CancellationToken cancellationToken)
        {
            try
            {
                var handle = Addressables.InstantiateAsync(key, parent);
                _handles.Add(handle);

                var gameObject = await handle.ToUniTask(cancellationToken: cancellationToken);
                AssetVaultOperationGuard.ThrowIfFailed(handle, $"Failed to instantiate asset by key '{key}'.");
                return gameObject;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw AssetVaultOperationGuard.ToAssetVaultException(exception, $"Failed to instantiate asset by key '{key}'.");
            }
        }

        /// <summary>
        /// この scope 経由でロードした全 handle を解放し、画面 lifetime が asset lifetime を所有するようにします。
        /// </summary>
        public void Dispose()
        {
            foreach (var handle in _handles)
            {
                Addressables.Release(handle);
            }

            _handles.Clear();
        }

    }
}
