using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.MissingChecker
{
    public class ProjectMissingCheckerSettings : ScriptableObject
    {
        private const string _settingsAssetPath = "Assets/Generated/UniCore/MissingCheckerSettings.asset";
        [SerializeField] private string _extensionsCsv = "prefab,asset,mat,controller,overrideController,playable,unity,anim,prefabvariant,shadergraph,asmdef,asmref";
        [SerializeField] private List<DefaultAsset> _targetFolders = new();
        [SerializeField] private bool _enableHierarchyHighlight = true;
        [SerializeField] private Color _hierarchyParentBackgroundColor = new(1f, 1f, 0f, 0.25f);
        [SerializeField] private Color _hierarchySelfBackgroundColor = new(1f, 1f, 0f, 0.25f);
        [SerializeField] private Color _hierarchyIconColor = Color.yellow;
        [SerializeField] private Color _projectSelfBackgroundColor = new(1f, 1f, 0f, 0.25f);
        [SerializeField] private Color _projectParentBackgroundColor = new(1f, 1f, 0f, 0.18f);

        private static ProjectMissingCheckerSettings _instance;

        public static void SetActive(ProjectMissingCheckerSettings settings)
        {
            _instance = settings;
            RepaintHierarchyAndProject();
        }

        public static ProjectMissingCheckerSettings GetOrCreate()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = AssetDatabase.LoadAssetAtPath<ProjectMissingCheckerSettings>(_settingsAssetPath);
            if (_instance == null)
            {
                _instance = FindAnySettingsAsset();
            }

            return _instance;
        }

        private static ProjectMissingCheckerSettings FindAnySettingsAsset()
        {
            var guids = AssetDatabase.FindAssets("t:ProjectMissingCheckerSettings");
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<ProjectMissingCheckerSettings>(path);
        }

        private static void RepaintHierarchyAndProject()
        {
            EditorApplication.RepaintHierarchyWindow();
            var method = typeof(EditorApplication).GetMethod("RepaintProjectWindow",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }

        public string ExtensionsCsv
        {
            get => _extensionsCsv;
            set => _extensionsCsv = value;
        }

        public List<DefaultAsset> TargetFolders => _targetFolders;

        public Color ProjectSelfBackgroundColor
        {
            get => _projectSelfBackgroundColor;
            set => _projectSelfBackgroundColor = value;
        }

        public Color ProjectParentBackgroundColor
        {
            get => _projectParentBackgroundColor;
            set => _projectParentBackgroundColor = value;
        }

        public bool EnableHierarchyHighlight
        {
            get => _enableHierarchyHighlight;
            set => _enableHierarchyHighlight = value;
        }

        public Color HierarchyParentBackgroundColor
        {
            get => _hierarchyParentBackgroundColor;
            set => _hierarchyParentBackgroundColor = value;
        }

        public Color HierarchySelfBackgroundColor
        {
            get => _hierarchySelfBackgroundColor;
            set => _hierarchySelfBackgroundColor = value;
        }

        public Color HierarchyIconColor
        {
            get => _hierarchyIconColor;
            set => _hierarchyIconColor = value;
        }

        public void SaveAsset()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}