using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.Persistence;

namespace UniLab.Network
{
    /// <summary>
    /// Persistent wrapper for all queued entries. Top-level class is required because
    /// JsonUtility does not serialize types nested inside generic classes.
    /// </summary>
    [Serializable]
    public sealed class OfflineQueueData<T>
    {
        /// <summary>Ordered list of pending entries.</summary>
        public List<OfflineQueueEntry<T>> Entries = new();
    }

    /// <summary>
    /// A single queued item with metadata for deduplication and ordering.
    /// </summary>
    [Serializable]
    public sealed class OfflineQueueEntry<T>
    {
        /// <summary>Caller-supplied key used to deduplicate entries on re-enqueue.</summary>
        public string IdempotencyKey;

        /// <summary>The payload to deliver when flushing.</summary>
        public T Payload;

        /// <summary>UTC Unix timestamp (milliseconds) when this entry was enqueued.</summary>
        public long EnqueuedAt;
    }

    /// <summary>
    /// Persists outgoing requests locally and flushes them when connectivity is restored.
    /// Uses <see cref="UniLab.Persistence.LocalSave"/> for durability across app restarts.
    /// Idempotency keys prevent duplicate submissions on retry.
    /// <para>
    /// IMPORTANT: <typeparamref name="T"/> must be a concrete, non-generic, <c>[Serializable]</c> class
    /// because persistence relies on <c>JsonUtility</c>, which does not support open generic types.
    /// </para>
    /// </summary>
    public sealed class OfflineQueue<T>
    {
        // --- Fields ---

        private OfflineQueueData<T> _data;

        // --- Properties ---

        /// <summary>Number of entries currently waiting to be flushed.</summary>
        public int Count => _data.Entries.Count;

        // --- Constructor ---

        /// <summary>
        /// Loads any previously persisted entries from <see cref="UniLab.Persistence.LocalSave"/> on construction.
        /// </summary>
        public OfflineQueue()
        {
            _data = LocalSave.Load<OfflineQueueData<T>>();
        }

        // --- Public API ---

        /// <summary>
        /// Adds a new entry to the queue. Silently ignores duplicate <paramref name="idempotencyKey"/> values
        /// to prevent double-submission when the same operation is enqueued more than once.
        /// </summary>
        public void Enqueue(string idempotencyKey, T payload)
        {
            var alreadyQueued = _data.Entries.Any(entry => entry.IdempotencyKey == idempotencyKey);
            if (alreadyQueued)
            {
                return;
            }

            _data.Entries.Add(new OfflineQueueEntry<T>
            {
                IdempotencyKey = idempotencyKey,
                Payload = payload,
                EnqueuedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            Persist();
        }

        /// <summary>
        /// Delivers every queued entry to <paramref name="handler"/> in enqueue order.
        /// Successfully processed entries are removed and the queue is persisted after each removal
        /// so a crash mid-flush does not re-submit already-delivered items.
        /// If <paramref name="handler"/> throws, the entry is kept and the flush continues with remaining entries.
        /// </summary>
        public async UniTask FlushAsync(
            Func<T, CancellationToken, UniTask> handler,
            CancellationToken cancellationToken = default)
        {
            // Snapshot to avoid mutating the collection while iterating.
            var snapshot = _data.Entries.ToList();

            foreach (var entry in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await handler(entry.Payload, cancellationToken);

                    _data.Entries.Remove(entry);
                    Persist();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Keep the entry so it can be retried on the next flush.
                    // Callers should log the exception before it propagates here if needed.
                }
            }
        }

        /// <summary>
        /// Removes all entries from the queue and persists the empty state.
        /// </summary>
        public void Clear()
        {
            _data.Entries.Clear();
            Persist();
        }

        // --- Private helpers ---

        private void Persist()
        {
            LocalSave.Save(_data);
        }
    }
}
