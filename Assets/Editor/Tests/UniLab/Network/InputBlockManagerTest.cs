using NUnit.Framework;
using R3;
using UniLab.UI;

namespace UniLab.Tests.EditMode.Network
{
    public class InputBlockManagerTest
    {
        [TearDown]
        public void TearDown()
        {
            // Dispose all blocks so static state is clean for the next test.
            InputBlockManager.ForceReleaseAllInputBlocks();
        }

        [Test]
        public void BlockedInput_IsFalseInitially()
        {
            InputBlockManager.ForceReleaseAllInputBlocks();

            Assert.IsFalse(InputBlockManager.BlockedInput);
        }

        [Test]
        public void CreateInputBlockWithLoading_BlocksInput()
        {
            using var block = InputBlockManager.CreateInputBlockWithLoading();

            Assert.IsTrue(InputBlockManager.BlockedInput);
        }

        [Test]
        public void Dispose_LastBlock_UnblocksInput()
        {
            var block = InputBlockManager.CreateInputBlockWithLoading();
            block.Dispose();

            Assert.IsFalse(InputBlockManager.BlockedInput);
        }

        [Test]
        public void MultipleBlocks_DisposeOne_StillBlocked()
        {
            var blockA = InputBlockManager.CreateInputBlockWithLoading();
            var blockB = InputBlockManager.CreateInputBlockWithLoading();

            blockA.Dispose();

            Assert.IsTrue(InputBlockManager.BlockedInput);

            blockB.Dispose();
        }

        [Test]
        public void MultipleBlocks_DisposeAll_Unblocked()
        {
            var blockA = InputBlockManager.CreateInputBlockWithLoading();
            var blockB = InputBlockManager.CreateInputBlockWithLoading();

            blockA.Dispose();
            blockB.Dispose();

            Assert.IsFalse(InputBlockManager.BlockedInput);
        }

        [Test]
        public void CreateInputBlockWithLoading_FiresOnShowLoadingObservable()
        {
            var fired = false;
            using var subscription = InputBlockManager.OnShowLoading.Subscribe(_ => fired = true);

            using var block = InputBlockManager.CreateInputBlockWithLoading();

            Assert.IsTrue(fired);
        }

        [Test]
        public void DisposeBlock_FiresOnHideLoadingObservable()
        {
            var hideCount = 0;
            using var subscription = InputBlockManager.OnHideLoading.Subscribe(_ => hideCount++);

            var block = InputBlockManager.CreateInputBlockWithLoading();
            block.Dispose();

            Assert.AreEqual(1, hideCount);
        }
    }
}
