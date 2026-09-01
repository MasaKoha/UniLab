using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UniLab.Diagnostics.Editor
{
    /// <summary>
    /// シーン階層ダンプを JSON へ保存するエディタメニューです。
    /// </summary>
    public static class SceneHierarchyDumperMenu
    {
        private const string MenuPath = "UniLab/Debug/Dump Scene Hierarchy";
        private const string FileNamePrefix = "hierarchy-";
        private const string FileNameTimestampFormat = "yyyyMMdd-HHmmss";
        private const string FileExtension = ".json";

        /// <summary>
        /// ロード済みシーンの階層ダンプを実行し JSON 保存します。
        /// </summary>
        [MenuItem(MenuPath)]
        private static void DumpSceneHierarchy()
        {
            var dump = SceneHierarchyDumper.Dump();
            Directory.CreateDirectory(DebugOutputPath.DirectoryPath);

            var timestamp = DateTime.Now.ToString(FileNameTimestampFormat);
            var outputFilePath = Path.Combine(DebugOutputPath.DirectoryPath, $"{FileNamePrefix}{timestamp}{FileExtension}");
            var json = JsonUtility.ToJson(dump, true);
            File.WriteAllText(outputFilePath, json);

            var nodeCount = CountNodes(dump);
            UnityEngine.Debug.Log($"シーン階層ダンプが完了しました。 nodes={nodeCount}, path={outputFilePath}");
        }

        private static int CountNodes(SceneHierarchyDump dump)
        {
            if (dump.scenes == null)
            {
                return 0;
            }

            var totalNodeCount = 0;
            for (var sceneIndex = 0; sceneIndex < dump.scenes.Length; sceneIndex++)
            {
                var scene = dump.scenes[sceneIndex];
                if (scene.nodes == null)
                {
                    continue;
                }

                totalNodeCount += scene.nodes.Length;
            }

            return totalNodeCount;
        }
    }
}
