using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.AssetReferenceFinder
{
    public class AssetReferenceFinderSettings : ScriptableObject
    {
        private const string _settingsAssetPath = "Assets/Generated/UniCore/AssetReferenceFinderSettings.asset";
        [SerializeField] private List<DefaultAsset> _targetFolders = new();
        public List<DefaultAsset> TargetFolders => _targetFolders;
        [field: SerializeField] public string ExtensionsCsv { get; set; } = "prefab,asset,mat,controller,overrideController,playable,unity,anim,prefabvariant,shadergraph,asmdef,asmref";
        [field: SerializeField] public Color ProjectReferenceBackgroundColor { get; set; } = new(1f, 1f, 0f, 0.25f);

        private static AssetReferenceFinderSettings _instance;

        public static void SetActive(AssetReferenceFinderSettings settings)
        {
            _instance = settings;
            RepaintProjectWindow();
        }

        public static AssetReferenceFinderSettings GetOrCreate()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = AssetDatabase.LoadAssetAtPath<AssetReferenceFinderSettings>(_settingsAssetPath);
            if (_instance == null)
            {
                _instance = FindAnySettingsAsset();
            }

            if (_instance == null)
            {
                _instance = CreateSettingsAsset();
            }

            return _instance;
        }

        private static AssetReferenceFinderSettings FindAnySettingsAsset()
        {
            var guids = AssetDatabase.FindAssets("t:AssetReferenceFinderSettings");
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<AssetReferenceFinderSettings>(path);
        }

        private static AssetReferenceFinderSettings CreateSettingsAsset()
        {
            var folderPath = Path.GetDirectoryName(_settingsAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = "Assets/Generated/UniCore";
            }

            EnsureFolderExists(folderPath);
            var settings = CreateInstance<AssetReferenceFinderSettings>();
            AssetDatabase.CreateAsset(settings, _settingsAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var name = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent))
            {
                parent = "Assets";
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        private static void RepaintProjectWindow()
        {
            var method = typeof(EditorApplication).GetMethod("RepaintProjectWindow",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }

        public void SaveAsset()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}