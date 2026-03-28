using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniLab.Network;
using UnityEngine;

namespace UniLab.Tests.EditMode.Network
{
    /// <summary>
    /// EditMode tests for OfflineQueue.
    /// PlayerPrefs is used for persistence, so each test clears the stored state via Clear().
    /// </summary>
    public class OfflineQueueTest
    {
        private OfflineQueue<TestRequest> _queue;

        [SetUp]
        public void SetUp()
        {
            _queue = new OfflineQueue<TestRequest>();
            _queue.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _queue.Clear();
        }

        [Test]
        public void Enqueue_IncreasesCount()
        {
            _queue.Enqueue("key1", new TestRequest { Url = "https://example.com" });

            Assert.AreEqual(1, _queue.Count);
        }

        [Test]
        public void Enqueue_SameIdempotencyKey_DoesNotDuplicate()
        {
            _queue.Enqueue("key1", new TestRequest { Url = "https://example.com/a" });
            _queue.Enqueue("key1", new TestRequest { Url = "https://example.com/b" });

            Assert.AreEqual(1, _queue.Count);
        }

        [Test]
        public void Enqueue_DifferentKeys_AllAdded()
        {
            _queue.Enqueue("key1", new TestRequest { Url = "a" });
            _queue.Enqueue("key2", new TestRequest { Url = "b" });
            _queue.Enqueue("key3", new TestRequest { Url = "c" });

            Assert.AreEqual(3, _queue.Count);
        }

        [Test]
        public void Clear_ResetsCountToZero()
        {
            _queue.Enqueue("key1", new TestRequest { Url = "a" });
            _queue.Enqueue("key2", new TestRequest { Url = "b" });
            _queue.Clear();

            Assert.AreEqual(0, _queue.Count);
        }

        [Test]
        public void FlushAsync_SuccessfulHandler_RemovesEntries()
        {
            _queue.Enqueue("key1", new TestRequest { Url = "a" });
            _queue.Enqueue("key2", new TestRequest { Url = "b" });

            _queue.FlushAsync((request, cancellationToken) => UniTask.CompletedTask)
                .Forget();

            // FlushAsync on an empty queue is synchronous in practice for completed tasks.
            // Use a simple synchronous workaround: run via UniTask's synchronous runner.
            var task = _queue.FlushAsync((_, _) => UniTask.CompletedTask);
            task.GetAwaiter().GetResult();

            // The queue should already be cleared from the first flush above too, but
            // the second flush was the one awaited to completion.
            Assert.AreEqual(0, _queue.Count);
        }

        [Test]
        public void FlushAsync_ThrowingHandler_KeepsEntry()
        {
            _queue.Enqueue("fail_key", new TestRequest { Url = "a" });

            var task = _queue.FlushAsync((_, _) => throw new Exception("handler failed"));
            task.GetAwaiter().GetResult();

            Assert.AreEqual(1, _queue.Count);
        }

        [Test]
        public void FlushAsync_InvokesHandlerForEachEntry()
        {
            _queue.Enqueue("key1", new TestRequest { Url = "a" });
            _queue.Enqueue("key2", new TestRequest { Url = "b" });
            _queue.Enqueue("key3", new TestRequest { Url = "c" });

            var handledUrls = new List<string>();
            _queue.FlushAsync((request, _) =>
            {
                handledUrls.Add(request.Url);
                return UniTask.CompletedTask;
            }).GetAwaiter().GetResult();

            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, handledUrls);
        }

        [Test]
        public void FlushAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            _queue.Enqueue("key1", new TestRequest { Url = "a" });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                _queue.FlushAsync((_, cancellationToken) => UniTask.CompletedTask, cts.Token)
                    .GetAwaiter()
                    .GetResult();
            });
        }

        [Serializable]
        public class TestRequest
        {
            public string Url;
        }
    }
}
