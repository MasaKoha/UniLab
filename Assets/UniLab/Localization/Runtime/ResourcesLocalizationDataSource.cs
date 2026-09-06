using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UniLab.Localization
{
    public sealed class ResourcesLocalizationDataSource : ILocalizationDataSource
    {
        public const string DefaultResourcePath = "LocalizationData";

        private readonly string _resourcePath;

        public ResourcesLocalizationDataSource(string resourcePath = DefaultResourcePath)
        {
            _resourcePath = string.IsNullOrWhiteSpace(resourcePath) ? DefaultResourcePath : resourcePath.Trim();
        }

        public LocalizationData Load()
        {
            var data = Resources.Load<LocalizationData>(_resourcePath);
#if UNITY_EDITOR
            if (data == null && _resourcePath == DefaultResourcePath)
            {
                var guids = AssetDatabase.FindAssets("t:LocalizationData");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    data = AssetDatabase.LoadAssetAtPath<LocalizationData>(path);
                }
            }
#endif

            return data;
        }
    }
}
