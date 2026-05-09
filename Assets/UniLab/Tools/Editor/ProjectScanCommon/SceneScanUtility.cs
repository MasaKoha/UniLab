using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.Tools.Editor.ProjectScanCommon
{
    /// <summary>
    /// Provides shared scene additive-open / close patterns used by project scan tools.
    /// </summary>
    public static class SceneScanUtility
    {
        /// <summary>
        /// Opens a scene additively if not loaded, executes the processor, then closes if it was opened.
        /// </summary>
        public static T ProcessScene<T>(string scenePath, Func<Scene, T> processor)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[SceneScanUtility] Scene not found, skipping: {scenePath}");
                return default;
            }

            var scene = SceneManager.GetSceneByPath(scenePath);
            var openedAdditively = false;

            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            try
            {
                return processor(scene);
            }
            finally
            {
                if (openedAdditively)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// Opens a scene additively if not loaded, executes the processor, then closes if it was opened.
        /// </summary>
        public static void ProcessScene(string scenePath, Action<Scene> processor)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[SceneScanUtility] Scene not found, skipping: {scenePath}");
                return;
            }

            var scene = SceneManager.GetSceneByPath(scenePath);
            var openedAdditively = false;

            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            try
            {
                processor(scene);
            }
            finally
            {
                if (openedAdditively)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// Processes all specified scene paths with progress bar support.
        /// Returns true if completed without cancellation.
        /// </summary>
        public static bool ProcessAllScenes(
            IReadOnlyList<string> scenePaths,
            string progressTitle,
            Action<Scene, string> processor)
        {
            try
            {
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    var scenePath = scenePaths[i];
                    if (EditorUtility.DisplayCancelableProgressBar(
                            progressTitle,
                            scenePath,
                            (float)i / scenePaths.Count))
                    {
                        return false;
                    }

                    ProcessScene(scenePath, scene =>
                    {
                        processor(scene, scenePath);
                    });
                }

                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
