#if UNITY_EDITOR
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>描画やシーンに依存せず可視面積比を検証します。</summary>
    public sealed class UiVisibilityUtilityTest
    {
        /// <summary>包含・半分・非交差を同じ矩形表現で判定します。</summary>
        [TestCase(0f, 1f)]
        [TestCase(5f, 0.5f)]
        [TestCase(10f, 0f)]
        [TestCase(20f, 0f)]
        public void ComputeVisibleRatioMatchesIntersection(float clipLeft, float expectedRatio)
        {
            var elementRect = new[] { 0f, 0f, 10f, 10f };
            var clipRect = new[] { clipLeft, 0f, 10f, 10f };
            Assert.That(UiVisibilityUtility.ComputeVisibleRatio(elementRect, clipRect), Is.EqualTo(expectedRatio));
        }

        /// <summary>面積ゼロで除算しないことを保証します。</summary>
        [TestCase(0f, 10f)]
        [TestCase(10f, 0f)]
        public void ComputeVisibleRatioReturnsZeroForZeroArea(float width, float height)
        {
            var elementRect = new[] { 0f, 0f, width, height };
            var clipRect = new[] { 0f, 0f, 10f, 10f };
            Assert.That(UiVisibilityUtility.ComputeVisibleRatio(elementRect, clipRect), Is.Zero);
        }
    }
}
#endif
