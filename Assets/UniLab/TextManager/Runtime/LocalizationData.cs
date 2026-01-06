using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace UniLab.TextManager
{
    [CreateAssetMenu(menuName = "Localization/LocalizationData")]
    public class LocalizationData : ScriptableObject
    {
        public List<string> Languages;
        public List<LocalizationEntry> Entries;

        private Dictionary<uint, LocalizationEntry> _hashMap;
        private Dictionary<uint, int> _languageHashToIndex;

        public void BuildHashMap()
        {
            _hashMap = Entries.ToDictionary(e => e.Hash, e => e);
        }

        public void BuildLanguageMap()
        {
            _languageHashToIndex = new Dictionary<uint, int>();
            for (var i = 0; i < Languages.Count; i++)
            {
                var hash = KeyHash.Fnv1AHash(Languages[i]);
                _languageHashToIndex[hash] = i;
            }
        }

        public string Get(uint keyHash, uint languageHash)
        {
            if (_hashMap == null)
            {
                BuildHashMap();
            }

            if (_languageHashToIndex == null)
            {
                BuildLanguageMap();
            }

            var langIndex = 0;
            if (_languageHashToIndex != null && !_languageHashToIndex.TryGetValue(languageHash, out langIndex))
            {
                return $"[MissingLangHash:{languageHash}]";
            }

            LocalizationEntry entry = null;
            if (_hashMap != null && !_hashMap.TryGetValue(keyHash, out entry))
            {
                return $"[MissingKeyHash:{keyHash}]";
            }

            if (entry != null && langIndex >= entry.Values.Count)
            {
                return $"[MissingValue:{langIndex}]";
            }

            return entry != null ? entry.Values[langIndex] : string.Empty;
        }
    }
}