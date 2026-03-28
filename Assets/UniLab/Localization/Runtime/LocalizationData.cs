using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Pre-warms both lookup dictionaries. Optional — Get() also builds them lazily on first call.
        /// Call this after loading the asset to avoid a hitch on the first text lookup.
        /// </summary>
        public void WarmupCache()
        {
            lock (_cacheLock)
            {
                _hashMap ??= Entries.ToDictionary(e => e.Hash, e => e);
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
            lock (_cacheLock)
            {
                _hashMap ??= Entries.ToDictionary(e => e.Hash, e => e);
                _languageHashToIndex ??= BuildLanguageHashToIndex();
            }

            if (!_languageHashToIndex.TryGetValue(languageHash, out var langIndex))
            {
                return $"[MissingLangHash:{languageHash}]";
            }

            if (!_hashMap.TryGetValue(keyHash, out var entry))
            {
                return $"[MissingKeyHash:{keyHash}]";
            }

            if (langIndex >= entry.Values.Count)
            {
                return $"[MissingValue:{langIndex}]";
            }

            return entry.Values[langIndex];
        }

        private Dictionary<uint, int> BuildLanguageHashToIndex()
        {
            var map = new Dictionary<uint, int>(Languages.Count);
            for (var i = 0; i < Languages.Count; i++)
            {
                map[KeyHash.Fnv1AHash(Languages[i])] = i;
            }

            return map;
        }
    }
}
