using System.Collections.Generic;
using NUnit.Framework;
using UniLab.TextManager;
using UnityEngine;

namespace UniLab.Tests.EditMode.TextManager
{
    public class LocalizationDataTest
    {
        private LocalizationData CreateData(
            List<string> languages,
            List<LocalizationEntry> entries)
        {
            var data = ScriptableObject.CreateInstance<LocalizationData>();
            data.Languages = languages;
            data.Entries = entries;
            return data;
        }

        private static LocalizationEntry Entry(string key, params string[] values)
        {
            return new LocalizationEntry
            {
                Key = key,
                Hash = KeyHash.Fnv1AHash(key),
                Values = new List<string>(values)
            };
        }

        [Test]
        public void Get_ReturnsCorrectTranslation()
        {
            var data = CreateData(
                new List<string> { "ja", "en" },
                new List<LocalizationEntry> { Entry("greeting", "こんにちは", "Hello") });

            var keyHash = KeyHash.Fnv1AHash("greeting");
            var jaHash = KeyHash.Fnv1AHash("ja");
            var enHash = KeyHash.Fnv1AHash("en");

            Assert.AreEqual("こんにちは", data.Get(keyHash, jaHash));
            Assert.AreEqual("Hello", data.Get(keyHash, enHash));
        }

        [Test]
        public void Get_MissingKey_ReturnsMissingKeyPlaceholder()
        {
            var data = CreateData(
                new List<string> { "ja" },
                new List<LocalizationEntry>());

            var result = data.Get(KeyHash.Fnv1AHash("nonexistent"), KeyHash.Fnv1AHash("ja"));

            StringAssert.StartsWith("[MissingKeyHash:", result);
        }

        [Test]
        public void Get_MissingLanguage_ReturnsMissingLangPlaceholder()
        {
            var data = CreateData(
                new List<string> { "ja" },
                new List<LocalizationEntry> { Entry("key", "値") });

            var result = data.Get(KeyHash.Fnv1AHash("key"), KeyHash.Fnv1AHash("fr"));

            StringAssert.StartsWith("[MissingLangHash:", result);
        }

        [Test]
        public void Get_MissingValue_ReturnsMissingValuePlaceholder()
        {
            // Entry has only 1 value (ja), but we request language index 1 (en)
            var data = CreateData(
                new List<string> { "ja", "en" },
                new List<LocalizationEntry> { Entry("key", "値") }); // only 1 value, no "en" entry

            var result = data.Get(KeyHash.Fnv1AHash("key"), KeyHash.Fnv1AHash("en"));

            StringAssert.StartsWith("[MissingValue:", result);
        }

        [Test]
        public void Get_CachesResultsAcrossCalls()
        {
            var data = CreateData(
                new List<string> { "ja" },
                new List<LocalizationEntry> { Entry("k", "v") });

            var hash = KeyHash.Fnv1AHash("k");
            var langHash = KeyHash.Fnv1AHash("ja");

            // Call twice — second call hits cache, result must be identical
            var first = data.Get(hash, langHash);
            var second = data.Get(hash, langHash);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void WarmupCache_ThenGet_ReturnsCorrectResult()
        {
            var data = CreateData(
                new List<string> { "ja" },
                new List<LocalizationEntry> { Entry("title", "タイトル") });

            data.WarmupCache();

            var result = data.Get(KeyHash.Fnv1AHash("title"), KeyHash.Fnv1AHash("ja"));
            Assert.AreEqual("タイトル", result);
        }

        [Test]
        public void InvalidateCache_ForcesRebuildOnNextGet()
        {
            var data = CreateData(
                new List<string> { "ja" },
                new List<LocalizationEntry> { Entry("k", "original") });

            var hash = KeyHash.Fnv1AHash("k");
            var langHash = KeyHash.Fnv1AHash("ja");

            // Warm up cache with "original"
            Assert.AreEqual("original", data.Get(hash, langHash));

            // Mutate entries and invalidate cache
            data.Entries[0].Values[0] = "updated";
            data.InvalidateCache();

            Assert.AreEqual("updated", data.Get(hash, langHash));
        }

        [Test]
        public void KeyHash_Fnv1A_IsStable()
        {
            // FNV-1a must be deterministic across calls
            Assert.AreEqual(KeyHash.Fnv1AHash("ja"), KeyHash.Fnv1AHash("ja"));
            Assert.AreNotEqual(KeyHash.Fnv1AHash("ja"), KeyHash.Fnv1AHash("en"));
        }

        [Test]
        public void Get_MultipleLanguages_AllResolveCorrectly()
        {
            var languages = new List<string> { "ja", "en", "zh" };
            var data = CreateData(
                languages,
                new List<LocalizationEntry> { Entry("hi", "こんにちは", "Hello", "你好") });

            var keyHash = KeyHash.Fnv1AHash("hi");

            Assert.AreEqual("こんにちは", data.Get(keyHash, KeyHash.Fnv1AHash("ja")));
            Assert.AreEqual("Hello", data.Get(keyHash, KeyHash.Fnv1AHash("en")));
            Assert.AreEqual("你好", data.Get(keyHash, KeyHash.Fnv1AHash("zh")));
        }
    }
}
