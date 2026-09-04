#if UNITY_EDITOR
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>可視フィルタとスクロール後の差分の契約を検証します。</summary>
    public sealed class UiSnapshotTest
    {
        /// <summary>既定圧縮・可視観測・全観測を区別します。</summary>
        [TestCase(null, true, false)]
        [TestCase("visible", false, false)]
        [TestCase("all", true, true)]
        public void CompactTextFiltersVisibility(string scope, bool includesClipped, bool includesOffscreen)
        {
            var snapshot = new UiSnapshotDocument
            {
                focusedPath = "Outside",
                elements = new[]
                {
                    new UiSnapshotElement { path = "Visible", name = "Visible", kind = "Button" },
                    new UiSnapshotElement { path = "Masked", name = "Masked", kind = "Button", clipped = true },
                    new UiSnapshotElement { path = "Outside", name = "Outside", kind = "Button", offscreen = true },
                },
            };
            var text = UiSnapshot.ToCompactText(snapshot, scope);
            Assert.That(text, Does.Contain("[Button] Visible"));
            Assert.That(text.Contains("Masked"), Is.EqualTo(includesClipped));
            Assert.That(text.Contains("[clipped]"), Is.EqualTo(includesClipped));
            Assert.That(text.Contains("Outside"), Is.EqualTo(includesOffscreen));
            Assert.That(snapshot.elements.Length, Is.EqualTo(3));
        }

        /// <summary>可視性だけが変わったスクロールも変更として通知します。</summary>
        [Test]
        public void CompareReportsVisibilityChanges()
        {
            var before = new UiSnapshotDocument
            {
                elements = new[] { new UiSnapshotElement { path = "Row", clipped = true, offscreen = true } },
            };
            var after = new UiSnapshotDocument
            {
                elements = new[] { new UiSnapshotElement { path = "Row" } },
            };
            var difference = UiSnapshot.Compare(before, after);
            Assert.That(difference.isEmpty, Is.False);
            Assert.That(difference.changed.Length, Is.EqualTo(2));
            Assert.That(difference.changed[0].field, Is.EqualTo("clipped"));
            Assert.That(difference.changed[1].field, Is.EqualTo("offscreen"));
        }
    }
}
#endif
