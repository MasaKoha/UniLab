using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UniLab.Localization
{
    public static class TextManager
    {
        private const string DefaultLanguage = "ja";

        // 既定は Resources 版のまま置く。SetDataSource を呼ばない既存の利用者を壊さないため。
        // 新しく組む側は AssetVaultLocalizationDataSource（Addressables）を SetDataSource で渡す。
        // Resources へ置いたアセットはビルドから剥がせず、起動時のインデックスにも載る。
        private static ILocalizationDataSource _dataSource = new ResourcesLocalizationDataSource();
        private static LocalizationData _data;
        private static string _currentLanguage = DefaultLanguage;
        private static uint _currentLangHash = KeyHash.Fnv1AHash(DefaultLanguage);
        private static readonly uint FallbackLangHash = KeyHash.Fnv1AHash(DefaultLanguage);

        public static event Action OnLanguageChanged;

        public static string CurrentLanguage => _currentLanguage;

        public static IReadOnlyList<string> SupportedLanguages
        {
            get
            {
                LoadLocalizeAsset();
                return _data?.SupportedLanguages ?? Array.Empty<string>();
            }
        }

        public static void SetLanguage(string lang)
        {
            var nextLanguage = NormalizeLanguage(lang);
            LoadLocalizeAsset();

            if (_data != null && !_data.HasLanguage(nextLanguage))
            {
                Debug.LogWarning($"Localization language not found: {nextLanguage}. Falling back to {DefaultLanguage}.");
                nextLanguage = _data.HasLanguage(DefaultLanguage) ? DefaultLanguage : _currentLanguage;
            }

            if (_currentLanguage == nextLanguage)
            {
                return;
            }

            _currentLanguage = nextLanguage;
            _currentLangHash = KeyHash.Fnv1AHash(nextLanguage);
            OnLanguageChanged?.Invoke();
        }

        public static bool HasLanguage(string lang)
        {
            LoadLocalizeAsset();
            return _data != null && _data.HasLanguage(NormalizeLanguage(lang));
        }

        public static void SetData(LocalizationData data, bool notify = true)
        {
            SetDataSource(new StaticLocalizationDataSource(data), reloadImmediately: false);
            ApplyLoadedData(data);
            if (notify)
            {
                OnLanguageChanged?.Invoke();
            }
        }

        public static void SetDataSource(ILocalizationDataSource dataSource, bool reloadImmediately = true)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _data = null;

            if (reloadImmediately)
            {
                LoadLocalizeAsset();
                OnLanguageChanged?.Invoke();
            }
        }

        /// <summary>
        /// Resources から読む構成に切り替える。互換のために残している。
        /// 新規の実装では AssetVaultLocalizationDataSource を SetDataSource へ渡すこと。
        /// </summary>
        public static void UseResources(string resourcePath = ResourcesLocalizationDataSource.DefaultResourcePath, bool reloadImmediately = true)
        {
            SetDataSource(new ResourcesLocalizationDataSource(resourcePath), reloadImmediately);
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

            ApplyLoadedData(_dataSource.Load());

            if (_data != null)
            {
                return;
            }

            Debug.LogWarning("LocalizationData not found.");
        }

        private static void ApplyLoadedData(LocalizationData data)
        {
            _data = data;
            if (_data == null)
            {
                return;
            }

            _data.WarmupCache();
            if (!_data.Validate(out var issues))
            {
                foreach (var issue in issues)
                {
                    Debug.LogWarning($"LocalizationData validation: {issue}");
                }
            }

            EnsureCurrentLanguageExists();
        }

        private static void EnsureCurrentLanguageExists()
        {
            if (_data == null || _data.HasLanguage(_currentLanguage))
            {
                return;
            }

            var supportedLanguages = _data.SupportedLanguages;
            var nextLanguage = _data.HasLanguage(DefaultLanguage)
                ? DefaultLanguage
                : supportedLanguages.Count > 0 ? supportedLanguages[0] : _currentLanguage;

            if (_currentLanguage == nextLanguage)
            {
                return;
            }

            Debug.LogWarning($"Current localization language not found: {_currentLanguage}. Falling back to {nextLanguage}.");
            _currentLanguage = nextLanguage;
            _currentLangHash = KeyHash.Fnv1AHash(nextLanguage);
        }

        public static string GetByHash(uint keyHash)
        {
            LoadLocalizeAsset();
            return _data?.Get(keyHash, _currentLangHash, FallbackLangHash) ?? $"[Missing:{keyHash}]";
        }

        public static string GetText(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var hash = KeyHash.Fnv1AHash(key);
            return Format(GetByHash(hash), args);
        }

        public static string GetTextOrDefault(string key, string defaultText, params object[] args)
        {
            var text = GetText(key, args);
            return IsMissingText(text) ? defaultText : text;
        }

        public static string GetText<T>(T key, params object[] args) where T : Enum
        {
            return GetText(key.ToString(), args);
        }

        public static string GetTextOrDefault<T>(T key, string defaultText, params object[] args) where T : Enum
        {
            return GetTextOrDefault(key.ToString(), defaultText, args);
        }

        private static string NormalizeLanguage(string lang)
        {
            return string.IsNullOrWhiteSpace(lang) ? DefaultLanguage : lang.Trim();
        }

        private static string Format(string text, object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return text;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, text, args);
            }
            catch (FormatException exception)
            {
                Debug.LogWarning($"Localization format failed: {exception.Message}");
                return text;
            }
        }

        private static bool IsMissingText(string text)
        {
            return text.StartsWith("[Missing", StringComparison.Ordinal);
        }
    }
}
