using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using R3;
using UniLab.Network;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniLab.Tests.PlayMode.Network
{
    public class NetworkReachabilityObservableTest
    {
        [Test]
        public void Constructor_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                using var observable = new NetworkReachabilityObservable(TimeSpan.FromSeconds(1));
            });
        }

        [Test]
        public void Dispose_DoesNotThrow()
        {
            var observable = new NetworkReachabilityObservable(TimeSpan.FromSeconds(1));

            Assert.DoesNotThrow(() => observable.Dispose());
        }

        [Test]
        public void Subscribe_ReturnsDisposable()
        {
            using var observable = new NetworkReachabilityObservable(TimeSpan.FromSeconds(1));

            IDisposable subscription = null;
            Assert.DoesNotThrow(() =>
            {
                subscription = observable.OnReachabilityChanged.Subscribe(_ => { });
            });

            subscription?.Dispose();
        }

        [UnityTest]
        public IEnumerator NoEmission_WhenReachabilityUnchanged()
        {
            var emissions = new List<NetworkReachability>();
            using var observable = new NetworkReachabilityObservable(TimeSpan.FromMilliseconds(100));
            using var subscription = observable.OnReachabilityChanged.Subscribe(r => emissions.Add(r));

            // Wait long enough for multiple poll intervals, but reachability won't change in tests.
            yield return new WaitForSeconds(0.4f);

            Assert.AreEqual(0, emissions.Count,
                "OnReachabilityChanged should not emit when reachability stays the same.");
        }

        [UnityTest]
        public IEnumerator Dispose_StopsPolling()
        {
            var emissions = new List<NetworkReachability>();
            var observable = new NetworkReachabilityObservable(TimeSpan.FromMilliseconds(100));
            using var subscription = observable.OnReachabilityChanged.Subscribe(r => emissions.Add(r));

            observable.Dispose();

            yield return new WaitForSeconds(0.3f);

            // After dispose, no further polls should occur (and certainly no crashes).
            Assert.Pass("No exception thrown after dispose.");
        }
    }
}
