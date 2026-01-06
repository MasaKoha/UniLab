using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UniLab.Tools.Editor
{
    public static class BuildChecker
    {
        [MenuItem("UniLab/Tools/Build Check/iOS")]
        public static void CheckBuildIOS()
        {
            CheckPlatformBuild(BuildTarget.iOS);
        }

        [MenuItem("UniLab/Tools/Build Check/Android")]
        public static void CheckBuildAndroid()
        {
            CheckPlatformBuild(BuildTarget.Android);
        }

        private static void CheckPlatformBuild(BuildTarget target)
        {
            var targetName = target.ToString();
            Debug.Log($"Checking build for {targetName}...");

            // 現在のプラットフォームを保存
            var currentTarget = EditorUserBuildSettings.activeBuildTarget;
            var currentGroup = BuildPipeline.GetBuildTargetGroup(currentTarget);

            var scenes = EditorBuildSettings.scenes;
            var scenePaths = new string[scenes.Length];
            for (var i = 0; i < scenes.Length; i++)
            {
                scenePaths[i] = scenes[i].path;
            }

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = $"Temp/{targetName}Build",
                target = target,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(buildOptions);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"{targetName} build succeeded.");
            }
            else
            {
                Debug.LogError($"{targetName} build failed: {report.summary.totalErrors} errors.");
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        Debug.LogError($"Error: {message.content}");
                    }
                }
            }

            // 元のプラットフォームに戻す
            if (EditorUserBuildSettings.activeBuildTarget == currentTarget)
            {
                return;
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(currentGroup, currentTarget);
        }
    }
}