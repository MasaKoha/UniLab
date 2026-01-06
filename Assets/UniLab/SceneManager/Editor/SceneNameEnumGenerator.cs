using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityCore.Scene.Editor
{
#if UNITY_EDITOR
    public static class SceneNamesEnumGenerator
    {
        private const string OutputPath = "Assets/Generated/UniLab/Scene/SceneNames.cs";
        private const string Namespace = "UniLab.Scene.Generated";
        private const string ScenesFolderKey = "UniLab.Scene.Editor.ScenesFolder";
        public static string ScenesFolder => EditorPrefs.GetString(ScenesFolderKey, "Assets/_Hime/Scenes");

        public static void Generate()
        {
            var scenesFolder = ScenesFolder;
            if (!Directory.Exists(scenesFolder))
            {
                Debug.LogError($"Scenesフォルダが見つかりません: {scenesFolder}");
                return;
            }

            var sceneFiles = Directory.GetFiles(scenesFolder, "*.unity")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name)
                .ToArray();

            // --- Build Settings への自動登録 ---
            var scenePaths = Directory.GetFiles(scenesFolder, "*.unity")
                .OrderBy(path => Path.GetFileNameWithoutExtension(path) == "BootScene" ? 0 : 1)
                .ThenBy(path => path)
                .ToArray();
            var buildScenes = scenePaths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            EditorBuildSettings.scenes = buildScenes;
            Debug.Log("Build Settings に全シーンを登録しました。");

            var usedNames = new Dictionary<string, int>();
            var enumEntries = sceneFiles.Select(originalName =>
            {
                var safeName = Regex.Replace(originalName, @"[^a-zA-Z0-9_]", "_");
                if (Regex.IsMatch(safeName, @"^\d")) safeName = "_" + safeName;
                if (usedNames.TryAdd(safeName, 0))
                {
                    return safeName;
                }

                usedNames[safeName]++;
                safeName += $"_{usedNames[safeName]}";
                return safeName;
            });

            var enumBody = string.Join(",\n        ", enumEntries);

            var code = $@"// 自動生成ファイル
namespace {Namespace}
{{
    public enum SceneNames
    {{
        {enumBody},
    }}
}}
";
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? string.Empty);
            File.WriteAllText(OutputPath, code);
            AssetDatabase.Refresh();
            Debug.Log("SceneNames.cs を自動生成しました。");
        }

        [MenuItem("UniLab/Scene/SceneName Enum Generator")]
        public static void ShowWindow()
        {
            SceneNamesEnumGeneratorWindow.ShowWindow();
        }

        public static void SetScenesFolder(string path)
        {
            EditorPrefs.SetString(ScenesFolderKey, path);
        }
    }

    public class SceneNamesEnumGeneratorWindow : EditorWindow
    {
        private DefaultAsset _scenesFolderAsset;

        public static void ShowWindow()
        {
            var window = GetWindow<SceneNamesEnumGeneratorWindow>("SceneName Enum Generator");
            window.Show();
        }

        private void OnEnable()
        {
            var path = SceneNamesEnumGenerator.ScenesFolder;
            _scenesFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        }

        private void OnGUI()
        {
            GUILayout.Label("Scenes フォルダを指定してください", EditorStyles.boldLabel);
            _scenesFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Scenes Folder", _scenesFolderAsset, typeof(DefaultAsset), false);

            if (_scenesFolderAsset != null)
            {
                var path = AssetDatabase.GetAssetPath(_scenesFolderAsset);
                if (GUILayout.Button("保存"))
                {
                    SceneNamesEnumGenerator.SetScenesFolder(path);
                    EditorUtility.DisplayDialog("保存", "ScenesFolder のパスを保存しました", "OK");
                }
            }

            GUILayout.Space(10);

            if (GUILayout.Button("SceneNames.cs を生成"))
            {
                SceneNamesEnumGenerator.Generate();
            }
        }
    }

    public class SceneNamesEnumAutoGenerator : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var scenesFolder = SceneNamesEnumGenerator.ScenesFolder;
            var shouldRegenerate = importedAssets.Concat(deletedAssets).Concat(movedAssets).Concat(movedFromAssetPaths)
                .Any(path => path.StartsWith(scenesFolder) && path.EndsWith(".unity"));

            if (shouldRegenerate)
            {
                SceneNamesEnumGenerator.Generate();
            }
        }
    }
#endif
}