using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UniLab.Localization
{
    public static class StaticSceneTextLocalizer
    {
        private const string StaticKeyPrefix = "ui_static_";

        private static readonly Dictionary<TMP_Text, string> RegisteredKeys = new();
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TextManager.OnLanguageChanged += UpdateRegisteredTexts;
            ScanLoadedSceneTexts();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            ScanLoadedSceneTexts();
        }

        private static void ScanLoadedSceneTexts()
        {
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in texts)
            {
                RegisterAndUpdate(text);
            }
        }

        private static void RegisterAndUpdate(TMP_Text text)
        {
            if (text == null || RegisteredKeys.ContainsKey(text) || string.IsNullOrEmpty(text.text))
            {
                return;
            }

            var key = text.text.StartsWith(StaticKeyPrefix, StringComparison.Ordinal)
                ? text.text
                : StaticKeyPrefix + KeyHash.Fnv1AHash(text.text);
            var localized = TextManager.GetText(key);
            if (localized.StartsWith("[Missing", StringComparison.Ordinal))
            {
                return;
            }

            RegisteredKeys.Add(text, key);
            text.text = localized;
        }

        private static void UpdateRegisteredTexts()
        {
            var staleTexts = ListPool<TMP_Text>.Get();
            foreach (var pair in RegisteredKeys)
            {
                if (pair.Key == null)
                {
                    staleTexts.Add(pair.Key);
                    continue;
                }

                pair.Key.text = TextManager.GetText(pair.Value);
            }

            foreach (var staleText in staleTexts)
            {
                RegisteredKeys.Remove(staleText);
            }

            ListPool<TMP_Text>.Release(staleTexts);
            ScanLoadedSceneTexts();
        }
    }

    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new();

        public static List<T> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<T>();
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}
