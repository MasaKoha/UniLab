using System.Collections.Generic;
using UniLab.Tools.Editor.ProjectScanCommon;
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
            ProjectScanEditorUtility.RepaintProjectWindow();
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
                _instance = ProjectScanEditorUtility.FindSettingsAsset<AssetReferenceFinderSettings>();
            }

            if (_instance == null)
            {
                _instance = ProjectScanEditorUtility.CreateSettingsAsset<AssetReferenceFinderSettings>(_settingsAssetPath);
            }

            return _instance;
        }

        public void SaveAsset()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
