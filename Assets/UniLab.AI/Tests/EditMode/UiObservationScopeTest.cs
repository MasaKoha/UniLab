#if UNITY_EDITOR
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>PR4 の観測契約をシーンなしで検証します。</summary>
    public sealed class UiObservationScopeTest
    {
        /// <summary>遮蔽された文字は visible だけから除外し、操作要素は残します。</summary>
        [Test]
        public void VisibleRemovesBlockedTextAndPreservesSelectable()
        {
            var document = CreateDocument();
            var filtered = UiObservationScope.Filter(document, "visible");
            Assert.That(filtered.elements.Length, Is.EqualTo(1));
            Assert.That(filtered.elements[0].kind, Is.EqualTo("Button"));
            Assert.That(UiSnapshot.ToCompactText(document, "visible"), Does.Not.Contain("背面本文"));
            Assert.That(UiSnapshot.ToCompactText(document, "visible"), Does.Contain("blocked:Modal"));
            Assert.That(document.elements.Length, Is.EqualTo(2));
        }

        /// <summary>all は遮蔽された文字と遮蔽元を出力します。</summary>
        [Test]
        public void AllIncludesBlockedTextAnnotation()
        {
            var document = CreateDocument();
            Assert.That(UiObservationScope.Filter(document, "all").elements.Length, Is.EqualTo(2));
            Assert.That(UiSnapshot.ToCompactText(document, "all"), Does.Contain("背面本文」 blocked:Modal"));
        }

        private static UiSnapshotDocument CreateDocument()
        {
            return new UiSnapshotDocument
            {
                elements = new[]
                {
                    new UiSnapshotElement { kind = "Text", path = "Background/Text", label = "背面本文", blockedBy = "Modal" },
                    new UiSnapshotElement { kind = "Button", path = "Background/Button", label = "操作", blockedBy = "Modal", interactable = true },
                },
            };
        }
    }
}
#endif
