using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniLab.AssetVault
{
    /// <summary>
    /// 参照カウント＋TTL/LRU でアセットを共有・遅延解放する <see cref="IAssetVaultCache"/> の Addressables 実装です。
    /// 同一スレッド（メインスレッド）からの利用を前提とします。
    /// </summary>
    public sealed class AssetVaultCache : IAssetVaultCache, IDisposable
    {
        private readonly AssetVaultCacheSettings _settings;
        private readonly Func<float> _timeProvider;
        private readonly Dictionary<CacheKey, Entry> _entries = new();
        private readonly List<CacheKey> _reclaimBuffer = new();

        /// <summary>
        /// 設定（既定は <see cref="AssetVaultCacheSettings.Default"/>）と時刻プロバイダ（既定は realtimeSinceStartup）でキャッシュを作成します。
        /// timeProvider はテストで時間を制御するために差し替え可能です。
        /// </summary>
        public AssetVaultCache(AssetVaultCacheSettings settings = default, Func<float> timeProvider = null)
        {
            // default(struct) は TTL=0/Capacity=0 になるため、未指定は Default を採用する。
            _settings = settings.TtlSeconds <= 0f && settings.Capacity <= 0 ? AssetVaultCacheSettings.Default : settings;
            _timeProvider = timeProvider ?? (() => Time.realtimeSinceStartup);
        }

        /// <inheritdoc />
        public async UniTask<IAssetReference<T>> AcquireAsync<T>(string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new AssetVaultException("AssetVaultCache.AcquireAsync: key is null or empty.");
            }

            SweepExpired();

            var cacheKey = new CacheKey(key, typeof(T));
            if (!_entries.TryGetValue(cacheKey, out var entry))
            {
                AsyncOperationHandle handle = Addressables.LoadAssetAsync<T>(key);
                entry = new Entry(handle);
                _entries.Add(cacheKey, entry);
            }

            entry.RefCount++;

            try
            {
                await entry.Handle.ToUniTask(cancellationToken: cancellationToken);
                AssetVaultOperationGuard.ThrowIfFailed(entry.Handle, $"Failed to load asset by key '{key}'.");
            }
            catch (Exception exception)
            {
                // この Acquire 分を取り消す。失敗ロードは TTL を待たず即破棄する。
                entry.RefCount--;
                if (entry.RefCount <= 0)
                {
                    ReleaseEntry(cacheKey, entry);
                }

                if (exception is OperationCanceledException)
                {
                    throw;
                }

                throw AssetVaultOperationGuard.ToAssetVaultException(exception, $"Failed to load asset by key '{key}'.");
            }

            EnforceCapacity();
            return new AssetReference<T>(this, cacheKey, (T)entry.Handle.Result);
        }

        /// <inheritdoc />
        public void Trim()
        {
            SweepExpired();
            EnforceCapacity();
        }

        /// <summary>
        /// 全エントリを参照カウントに関わらず解放します。
        /// </summary>
        public void Dispose()
        {
            foreach (var pair in _entries)
            {
                if (pair.Value.Handle.IsValid())
                {
                    Addressables.Release(pair.Value.Handle);
                }
            }

            _entries.Clear();
        }

        // 参照を 1 つ手放す。0 になったら TTL に従い、TTL<=0 なら即解放、それ以外は猶予のため保持する。
        // 入れ子の AssetReference から呼ぶ（入れ子型は外側の private にアクセス可能）。
        private void ReleaseInternal(CacheKey cacheKey)
        {
            if (!_entries.TryGetValue(cacheKey, out var entry))
            {
                return;
            }

            entry.RefCount--;
            if (entry.RefCount > 0)
            {
                return;
            }

            entry.LastReleaseTime = _timeProvider();
            if (_settings.TtlSeconds <= 0f)
            {
                ReleaseEntry(cacheKey, entry);
            }
        }

        private void SweepExpired()
        {
            if (_settings.TtlSeconds <= 0f)
            {
                return;
            }

            var now = _timeProvider();
            _reclaimBuffer.Clear();
            foreach (var pair in _entries)
            {
                if (pair.Value.RefCount <= 0 && now - pair.Value.LastReleaseTime >= _settings.TtlSeconds)
                {
                    _reclaimBuffer.Add(pair.Key);
                }
            }

            foreach (var cacheKey in _reclaimBuffer)
            {
                ReleaseEntry(cacheKey, _entries[cacheKey]);
            }
        }

        private void EnforceCapacity()
        {
            if (_settings.Capacity <= 0 || _entries.Count <= _settings.Capacity)
            {
                return;
            }

            // 未参照エントリを「最後に手放した時刻が古い順」に解放して上限まで詰める。
            _reclaimBuffer.Clear();
            foreach (var pair in _entries)
            {
                if (pair.Value.RefCount <= 0)
                {
                    _reclaimBuffer.Add(pair.Key);
                }
            }

            _reclaimBuffer.Sort((left, right) => _entries[left].LastReleaseTime.CompareTo(_entries[right].LastReleaseTime));

            foreach (var cacheKey in _reclaimBuffer)
            {
                if (_entries.Count <= _settings.Capacity)
                {
                    break;
                }

                ReleaseEntry(cacheKey, _entries[cacheKey]);
            }
        }

        private void ReleaseEntry(CacheKey cacheKey, Entry entry)
        {
            _entries.Remove(cacheKey);
            if (entry.Handle.IsValid())
            {
                Addressables.Release(entry.Handle);
            }
        }

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly string _key;
            private readonly Type _type;

            public CacheKey(string key, Type type)
            {
                _key = key;
                _type = type;
            }

            public bool Equals(CacheKey other)
            {
                return _key == other._key && _type == other._type;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_key, _type);
            }
        }

        private sealed class Entry
        {
            public Entry(AsyncOperationHandle handle)
            {
                Handle = handle;
            }

            public AsyncOperationHandle Handle { get; }
            public int RefCount { get; set; }
            public float LastReleaseTime { get; set; }
        }

        private sealed class AssetReference<T> : IAssetReference<T>
        {
            private readonly AssetVaultCache _cache;
            private readonly CacheKey _cacheKey;
            private bool _disposed;

            public AssetReference(AssetVaultCache cache, CacheKey cacheKey, T value)
            {
                _cache = cache;
                _cacheKey = cacheKey;
                Value = value;
            }

            public T Value { get; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _cache.ReleaseInternal(_cacheKey);
            }
        }
    }
}
