using System.Collections.Generic;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.UnreferencedAssetFinder
{
    /// <summary>
    /// Stores settings for the unreferenced asset finder.
    /// </summary>
    public class UnreferencedAssetFinderSettings : ScriptableObject
    {
        private const string _settingsAssetPath = "Assets/Generated/UniCore/UnreferencedAssetFinderSettings.asset";

        [SerializeField] private string _extensionsCsv = "prefab,asset,mat,controller,overrideController,playable,unity,anim,prefabvariant,shadergraph";
        [SerializeField] private List<DefaultAsset> _targetFolders = new();
        [SerializeField] private bool _excludeResourcesFolder = true;
        [SerializeField] private bool _excludeStreamingAssetsFolder = true;
        [SerializeField] private bool _excludeBuildScenes = true;
        [SerializeField] private Color _projectSelfBackgroundColor = new(1f, 0.4f, 0.4f, 0.25f);
        [SerializeField] private Color _projectParentBackgroundColor = new(1f, 0.4f, 0.4f, 0.18f);

        private static UnreferencedAssetFinderSettings _instance;

        /// <summary>
        /// Gets or sets the comma-separated list of extensions to scan.
        /// </summary>
        public string ExtensionsCsv
        {
            get => _extensionsCsv;
            set => _extensionsCsv = value;
        }

        /// <summary>
        /// Gets the root folders that limit the scan target.
        /// </summary>
        public List<DefaultAsset> TargetFolders => _targetFolders;

        /// <summary>
        /// Gets or sets whether assets under Resources folders are excluded from results.
        /// </summary>
        public bool ExcludeResourcesFolder
        {
            get => _excludeResourcesFolder;
            set => _excludeResourcesFolder = value;
        }

        /// <summary>
        /// Gets or sets whether assets under StreamingAssets folders are excluded from results.
        /// </summary>
        public bool ExcludeStreamingAssetsFolder
        {
            get => _excludeStreamingAssetsFolder;
            set => _excludeStreamingAssetsFolder = value;
        }

        /// <summary>
        /// Gets or sets whether enabled build scenes are excluded from results.
        /// </summary>
        public bool ExcludeBuildScenes
        {
            get => _excludeBuildScenes;
            set => _excludeBuildScenes = value;
        }

        /// <summary>
        /// Gets or sets the project window background color for unreferenced assets.
        /// </summary>
        public Color ProjectSelfBackgroundColor
        {
            get => _projectSelfBackgroundColor;
            set => _projectSelfBackgroundColor = value;
        }

        /// <summary>
        /// Gets or sets the project window background color for parent folders.
        /// </summary>
        public Color ProjectParentBackgroundColor
        {
            get => _projectParentBackgroundColor;
            set => _projectParentBackgroundColor = value;
        }

        /// <summary>
        /// Sets the active settings asset used by windows and highlighters.
        /// </summary>
        public static void SetActive(UnreferencedAssetFinderSettings settings)
        {
            _instance = settings;
            ProjectScanEditorUtility.RepaintProjectWindow();
        }

        /// <summary>
        /// Gets the active settings asset or creates the default asset when none exists.
        /// </summary>
        public static UnreferencedAssetFinderSettings GetOrCreate()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = AssetDatabase.LoadAssetAtPath<UnreferencedAssetFinderSettings>(_settingsAssetPath);
            if (_instance == null)
            {
                _instance = ProjectScanEditorUtility.FindSettingsAsset<UnreferencedAssetFinderSettings>();
            }

            if (_instance == null)
            {
                _instance = ProjectScanEditorUtility.CreateSettingsAsset<UnreferencedAssetFinderSettings>(_settingsAssetPath);
            }

            return _instance;
        }

        /// <summary>
        /// Saves this settings asset.
        /// </summary>
        public void SaveAsset()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
