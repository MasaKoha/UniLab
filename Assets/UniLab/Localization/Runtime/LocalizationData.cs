using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniLab.Localization
{
    /// <summary>
    /// ScriptableObject that maps localization keys (FNV-1a hashes) to translated strings.
    /// Caches lookup dictionaries lazily on first Get() call, thread-safe via lock.
    /// Call InvalidateCache() after modifying Entries or Languages at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Localization/LocalizationData")]
    public class LocalizationData : ScriptableObject
    {
        public List<string> Languages;
        public List<LocalizationEntry> Entries;

        private Dictionary<uint, LocalizationEntry> _hashMap;
        private Dictionary<uint, int> _languageHashToIndex;

        // Guards lazy initialization of both dictionaries — safe for background thread access.
        private readonly object _cacheLock = new();

        public IReadOnlyList<string> SupportedLanguages
        {
            get
            {
                if (Languages != null)
                {
                    return Languages;
                }

                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Pre-warms both lookup dictionaries. Optional — Get() also builds them lazily on first call.
        /// Call this after loading the asset to avoid a hitch on the first text lookup.
        /// </summary>
        public void WarmupCache()
        {
            lock (_cacheLock)
            {
                _hashMap ??= BuildHashMap();
                _languageHashToIndex ??= BuildLanguageHashToIndex();
            }
        }

        /// <summary>
        /// Forces a full rebuild of both lookup dictionaries.
        /// Call this when Entries or Languages are changed at runtime (e.g. after an import).
        /// </summary>
        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _hashMap = null;
                _languageHashToIndex = null;
            }
        }

        /// <summary>
        /// Returns the translated string for the given key and language hashes.
        /// Lazily builds the lookup dictionaries on first call.
        /// </summary>
        public string Get(uint keyHash, uint languageHash)
        {
            return Get(keyHash, languageHash, 0);
        }

        public string Get(uint keyHash, uint languageHash, uint fallbackLanguageHash)
        {
            lock (_cacheLock)
            {
                _hashMap ??= BuildHashMap();
                _languageHashToIndex ??= BuildLanguageHashToIndex();
            }

            if (!_hashMap.TryGetValue(keyHash, out var entry))
            {
                return $"[MissingKeyHash:{keyHash}]";
            }

            if (TryGetValue(entry, languageHash, out var value))
            {
                return value;
            }

            if (fallbackLanguageHash != 0 &&
                fallbackLanguageHash != languageHash &&
                TryGetValue(entry, fallbackLanguageHash, out var fallbackValue))
            {
                return fallbackValue;
            }

            return $"[MissingValue:{keyHash}:{languageHash}]";
        }

        public bool HasLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return false;
            }

            lock (_cacheLock)
            {
                _languageHashToIndex ??= BuildLanguageHashToIndex();
            }

            return _languageHashToIndex.ContainsKey(KeyHash.Fnv1AHash(language.Trim()));
        }

        public bool Validate(out List<string> issues)
        {
            issues = new List<string>();

            if (Languages == null || Languages.Count == 0)
            {
                issues.Add("LocalizationData has no languages.");
            }
            else
            {
                var languageSet = new HashSet<string>();
                for (var i = 0; i < Languages.Count; i++)
                {
                    var language = Languages[i];
                    if (string.IsNullOrWhiteSpace(language))
                    {
                        issues.Add($"Language at index {i} is empty.");
                        continue;
                    }

                    if (!languageSet.Add(language))
                    {
                        issues.Add($"Duplicate language: {language}");
                    }
                }
            }

            if (Entries == null || Entries.Count == 0)
            {
                issues.Add("LocalizationData has no entries.");
                return issues.Count == 0;
            }

            var keySet = new HashSet<string>();
            var hashSet = new HashSet<uint>();
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null)
                {
                    issues.Add($"Entry at index {i} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    issues.Add($"Entry at index {i} has an empty key.");
                }
                else
                {
                    if (!keySet.Add(entry.Key))
                    {
                        issues.Add($"Duplicate key: {entry.Key}");
                    }

                    var expectedHash = KeyHash.Fnv1AHash(entry.Key);
                    if (entry.Hash != 0 && entry.Hash != expectedHash)
                    {
                        issues.Add($"Hash mismatch for key {entry.Key}: expected {expectedHash}, actual {entry.Hash}");
                    }
                }

                var effectiveHash = EffectiveHash(entry);
                if (!hashSet.Add(effectiveHash))
                {
                    issues.Add($"Duplicate hash: {effectiveHash}");
                }

                var expectedValueCount = Languages?.Count ?? 0;
                var actualValueCount = entry.Values?.Count ?? 0;
                if (actualValueCount != expectedValueCount)
                {
                    issues.Add($"Value count mismatch for key {entry.Key}: expected {expectedValueCount}, actual {actualValueCount}");
                }
            }

            return issues.Count == 0;
        }

        private Dictionary<uint, int> BuildLanguageHashToIndex()
        {
            var languages = Languages ?? new List<string>();
            var map = new Dictionary<uint, int>(languages.Count);
            for (var i = 0; i < languages.Count; i++)
            {
                var language = languages[i];
                if (string.IsNullOrWhiteSpace(language))
                {
                    continue;
                }

                var hash = KeyHash.Fnv1AHash(language.Trim());
                if (!map.ContainsKey(hash))
                {
                    map.Add(hash, i);
                }
            }

            return map;
        }

        private Dictionary<uint, LocalizationEntry> BuildHashMap()
        {
            var entries = Entries ?? new List<LocalizationEntry>();
            var map = new Dictionary<uint, LocalizationEntry>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                var hash = EffectiveHash(entry);
                if (!map.ContainsKey(hash))
                {
                    map.Add(hash, entry);
                }
            }

            return map;
        }

        private static uint EffectiveHash(LocalizationEntry entry)
        {
            return entry.Hash != 0 || string.IsNullOrEmpty(entry.Key)
                ? entry.Hash
                : KeyHash.Fnv1AHash(entry.Key);
        }

        private bool TryGetValue(LocalizationEntry entry, uint languageHash, out string value)
        {
            value = null;

            if (!_languageHashToIndex.TryGetValue(languageHash, out var langIndex))
            {
                return false;
            }

            if (entry.Values == null || langIndex >= entry.Values.Count)
            {
                return false;
            }

            value = entry.Values[langIndex];
            return !string.IsNullOrEmpty(value);
        }
    }
}
