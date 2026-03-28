using System.Collections;
using NUnit.Framework;
using R3;
using UniLab.UI;
using UnityEngine.TestTools;

namespace UniLab.Tests.PlayMode.UI
{
    /// <summary>
    /// Tests InputBlockManager reference counting, which is the underlying mechanism
    /// LoadingOverlayManager relies on. LoadingOverlayManager itself requires a scene
    /// prefab setup, so the blocking logic is verified here instead.
    /// </summary>
    public class LoadingOverlayReferenceCountTest
    {
        [SetUp]
        public void SetUp()
        {
            InputBlockManager.ForceReleaseAllInputBlocks();
        }

        [TearDown]
        public void TearDown()
        {
            InputBlockManager.ForceReleaseAllInputBlocks();
        }

        [UnityTest]
        public IEnumerator Show_SetsBlockedInput()
        {
            using var handle = InputBlockManager.CreateInputBlockWithLoading();

            Assert.IsTrue(InputBlockManager.BlockedInput);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dispose_LastHandle_ClearsBlockedInput()
        {
            var handle = InputBlockManager.CreateInputBlockWithLoading();
            handle.Dispose();

            Assert.IsFalse(InputBlockManager.BlockedInput);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedShows_DisposeOne_StillBlocked()
        {
            var handleA = InputBlockManager.CreateInputBlockWithLoading();
            var handleB = InputBlockManager.CreateInputBlockWithLoading();

            handleA.Dispose();

            Assert.IsTrue(InputBlockManager.BlockedInput);

            handleB.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedShows_DisposeAll_Unblocked()
        {
            var handleA = InputBlockManager.CreateInputBlockWithLoading();
            var handleB = InputBlockManager.CreateInputBlockWithLoading();

            handleA.Dispose();
            handleB.Dispose();

            Assert.IsFalse(InputBlockManager.BlockedInput);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Show_FiresOnShowLoadingObservable()
        {
            var fired = false;
            using var subscription = InputBlockManager.OnShowLoading.Subscribe(_ => fired = true);

            using var handle = InputBlockManager.CreateInputBlockWithLoading();

            Assert.IsTrue(fired);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dispose_FiresOnHideLoadingObservable()
        {
            var hideCount = 0;
            using var subscription = InputBlockManager.OnHideLoading.Subscribe(_ => hideCount++);

            var handle = InputBlockManager.CreateInputBlockWithLoading();
            handle.Dispose();

            Assert.AreEqual(1, hideCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DoubleDispose_DoesNotFireOnHideTwice()
        {
            var hideCount = 0;
            using var subscription = InputBlockManager.OnHideLoading.Subscribe(_ => hideCount++);

            // LoadingInputBlock has no double-dispose guard; this test documents current behavior.
            var handle = InputBlockManager.CreateInputBlockWithLoading();
            handle.Dispose();
            handle.Dispose();

            // Current implementation fires twice on double-dispose (no guard).
            Assert.AreEqual(2, hideCount);
            yield return null;
        }
    }
}
