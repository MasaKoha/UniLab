#if UNITY_EDITOR
using NUnit.Framework;

namespace UniLab.AI.Tests
{
    /// <summary>合成観測で検索と推奨対象指定の契約を検証します。</summary>
    public sealed class AgentFindTest
    {
        /// <summary>タグを除去して部分一致し、種別を絞り込みます。</summary>
        [TestCase(null, 2)]
        [TestCase("Button", 1)]
        [TestCase("Toggle", 0)]
        public void LabelSubstringAndKindFilter(string kind, int expectedCount)
        {
            var response = AgentFind.Find(CreateSnapshot(), "開始", kind);
            var count = response.text.Length == 0 ? 0 : response.text.Split('\n').Length;
            Assert.That(count, Is.EqualTo(expectedCount));
            Assert.That(response.text, Does.Not.Contain("<b>"));
            Assert.That(response.ok, Is.True);
        }

        /// <summary>絞り込み後に一件でも元の観測で同名ならラベル指定を推奨します。</summary>
        [Test]
        public void DuplicateNamesRecommendLabelTargetSpec()
        {
            var response = AgentFind.Find(CreateSnapshot(), "冒険", "Button");
            var expectedTarget = UiInputLocator.CreateLabelTargetSpec("冒険を開始", int.MaxValue);
            Assert.That(response.text, Does.Contain($"→ submit:\"{expectedTarget}\""));
            Assert.That(response.text, Does.Contain("rect=[10,20,30,40]"));
            Assert.That(response.text, Does.Contain("interactable=true blockedBy=\"\" clipped=false"));
        }

        /// <summary>ゼロ件でも要求は成功し、本文は空にします。</summary>
        [Test]
        public void NoMatchesReturnEmptyText()
        {
            var response = AgentFind.Find(CreateSnapshot(), "存在しない", null);
            Assert.That(response.ok, Is.True);
            Assert.That(response.text, Is.Empty);
            Assert.That(response.message, Is.EqualTo("見つかりません"));
        }

        /// <summary>全範囲ではマスク外の行も検索できます。</summary>
        [TestCase("visible", false)]
        [TestCase("all", true)]
        public void ScopeIncludesClippedRowsOnlyWhenAll(string scope, bool expectedFound)
        {
            var response = AgentFind.Find(CreateSnapshot(), "設定", null, scope);
            Assert.That(response.text.Length > 0, Is.EqualTo(expectedFound));
        }

        /// <summary>検索語彙の誤指定を黙ってゼロ件にしません。</summary>
        [Test]
        public void InvalidKindIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => AgentFind.Find(CreateSnapshot(), null, "Unknown"));
        }

        private static UiSnapshotDocument CreateSnapshot()
        {
            return new UiSnapshotDocument
            {
                elements = new[]
                {
                    new UiSnapshotElement { name = "Row", path = "Canvas/Row", kind = "Button", label = "<b>冒険</b>を開始", interactable = true, rect = new[] { 10f, 20f, 30f, 40f } },
                    new UiSnapshotElement { name = "Row", path = "Canvas/Row", kind = "Button", label = "設定", clipped = true },
                    new UiSnapshotElement { name = "Title", path = "Canvas/Title", kind = "Text", label = "開始画面" },
                },
            };
        }
    }
}
#endif
