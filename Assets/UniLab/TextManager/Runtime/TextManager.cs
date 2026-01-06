using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UniLab.TextManager
{
    public static class TextManager
    {
        private static LocalizationData _data;
        private static uint _currentLangHash = KeyHash.Fnv1AHash("ja");
        public static event Action OnLanguageChanged;

        public static void SetLanguage(string lang)
        {
            _currentLangHash = KeyHash.Fnv1AHash(lang);
            OnLanguageChanged?.Invoke();
        }

        public static void ResetLoadedAsset()
        {
            _data = null;
        }

        private static void LoadLocalizeAsset()
        {
            if (_data != null)
            {
                return;
            }

            _data = Resources.Load<LocalizationData>("LocalizationData");
#if UNITY_EDITOR
            if (_data == null)
            {
                var guids = AssetDatabase.FindAssets("t:LocalizationData");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _data = AssetDatabase.LoadAssetAtPath<LocalizationData>(path);
                }
            }
#endif

            if (_data != null)
            {
                _data.BuildHashMap();
                _data.BuildLanguageMap();
            }
            else
            {
                Debug.LogWarning("LocalizationData not found.");
            }
        }

        public static string GetByHash(uint keyHash)
        {
            LoadLocalizeAsset();
            return _data?.Get(keyHash, _currentLangHash) ?? $"[Missing:{keyHash}]";
        }

        public static string GetText(string key)
        {
            var hash = KeyHash.Fnv1AHash(key);
            return GetByHash(hash);
        }

        public static string GetText<T>(T key) where T : Enum
        {
            return GetText(key.ToString());
        }
    }
}