#if UNITY_EDITOR
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>フレーム番号を指定して観測共有の寿命を検証します。</summary>
    public sealed class UiSnapshotCacheTest
    {
        /// <summary>同一フレームは同じ参照、次フレームは別の参照を返します。</summary>
        [Test]
        public void SameFrameSharesReferenceAndNextFrameRecaptures()
        {
            const int FirstFrame = 100;
            const int NextFrame = 101;
            UiSnapshot.Capture();
            var first = UiSnapshot.Capture(FirstFrame);
            Assert.That(UiSnapshot.Capture(FirstFrame), Is.SameAs(first));
            var next = UiSnapshot.Capture(NextFrame);
            Assert.That(next, Is.Not.SameAs(first));
            Assert.That(next.frame, Is.EqualTo(NextFrame));
            UiSnapshot.Capture();
        }

        /// <summary>停止中の公開入口はキャッシュを使用しません。</summary>
        [Test]
        public void EditModeCaptureDoesNotCache()
        {
            Assert.That(UiSnapshot.Capture(), Is.Not.SameAs(UiSnapshot.Capture()));
        }
    }
}
#endif
