#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.AI
{
    /// <summary>
    /// シナリオステップから実行対象・待機アンカー・表示用ラベルを読み取ります。
    /// </summary>
    internal static class UiScenarioStepReader
    {
        private const int DefaultSettleFrames = 30;

        internal static string GetPrimaryTarget(UiScenarioStep step)
        {
            if (!string.IsNullOrEmpty(step.submit)) { return step.submit; }
            if (!string.IsNullOrEmpty(step.waitForObject)) { return step.waitForObject; }
            if (!string.IsNullOrEmpty(step.click)) { return step.click; }
            if (!string.IsNullOrEmpty(step.tap)) { return step.tap; }
            if (!string.IsNullOrEmpty(step.pointerMove)) { return step.pointerMove; }
            if (!string.IsNullOrEmpty(step.scroll)) { return step.scroll; }
            return string.Empty;
        }

        internal static string GetStepFailureTarget(UiScenarioStep step)
        {
            if (step == null)
            {
                return string.Empty;
            }

            var primaryTarget = GetPrimaryTarget(step);
            if (!string.IsNullOrEmpty(primaryTarget))
            {
                return primaryTarget;
            }

            if (!string.IsNullOrEmpty(step.waitScene))
            {
                return step.waitScene;
            }

            if (!string.IsNullOrEmpty(step.waitForScene))
            {
                return step.waitForScene;
            }

            if (!string.IsNullOrEmpty(step.capture))
            {
                return step.capture;
            }

            return string.Empty;
        }

        internal static UiScenarioStep EnsureStep(UiScenarioStep step)
        {
            return step ?? new UiScenarioStep();
        }

        internal static bool HasAnyAction(UiScenarioStep step)
        {
            return !string.IsNullOrEmpty(step.submit) || ScenarioInputExecutor.IsInputStep(step);
        }

        internal static string CreateActionLabel(UiScenarioStep step)
        {
            if (!string.IsNullOrEmpty(step.submit))
            {
                return $"submit:{step.submit}";
            }

            var inputKind = ScenarioInputExecutor.GetInputKind(step);
            if (!string.IsNullOrEmpty(inputKind))
            {
                return $"input:{inputKind}";
            }

            if (step.monkey != null)
            {
                return "monkey";
            }

            return "wait";
        }

        internal static int GetSettleFrameCount(UiScenarioStep step)
        {
            if (step.settleFrames > 0)
            {
                return step.settleFrames;
            }

            var needsSettledFrame = !string.IsNullOrEmpty(step.capture)
                || !string.IsNullOrEmpty(step.snapshot)
                || step.audit
                || (step.expect != null && step.expect.Length > 0);
            return needsSettledFrame ? DefaultSettleFrames : 0;
        }

        internal static InputReplayAnchor CreateAnchor(UiScenarioStep step)
        {
            var waitForScene = step.waitForScene;
            if (string.IsNullOrEmpty(waitForScene) && !HasAnyAction(step))
            {
                waitForScene = step.waitScene;
            }

            return new InputReplayAnchor
            {
                waitForText = step.waitForText,
                waitForObject = step.waitForObject,
                waitForFocus = step.waitForFocus,
                waitForScene = waitForScene,
            };
        }

        internal static bool EndsWithPath(string path, string target)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(target))
            {
                return false;
            }

            return path.EndsWith($"/{target}", StringComparison.Ordinal);
        }

        internal static string ResolveScenarioName(UiScenario scenario, string scenarioName)
        {
            if (!string.IsNullOrEmpty(scenarioName))
            {
                return scenarioName;
            }

            if (scenario != null && !string.IsNullOrEmpty(scenario.name))
            {
                return scenario.name;
            }

            return $"scenario-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
        }

        internal static string ResolveOutputDirectory(UiScenario scenario, string defaultOutputDirectoryName)
        {
            if (scenario != null && !string.IsNullOrEmpty(scenario.outputDirectory))
            {
                return scenario.outputDirectory;
            }

            return Path.Combine(DebugOutputPath.DirectoryPath, defaultOutputDirectoryName);
        }

        internal static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "scenario";
            }

            var sanitizedName = fileName;
            var invalidCharacters = Path.GetInvalidFileNameChars();
            for (var characterIndex = 0; characterIndex < invalidCharacters.Length; characterIndex++)
            {
                sanitizedName = sanitizedName.Replace(invalidCharacters[characterIndex], '_');
            }

            return string.IsNullOrEmpty(sanitizedName) ? "scenario" : sanitizedName;
        }

        internal static string ResolveReplayDirectory(string replayName, string replayDirectoryName)
        {
            if (Path.IsPathRooted(replayName))
            {
                return replayName;
            }

            return Path.Combine(DebugOutputPath.DirectoryPath, replayDirectoryName, replayName);
        }

        internal static UiSnapshotDocument CreateFallbackSnapshot()
        {
            return new UiSnapshotDocument
            {
                capturedAt = DateTimeOffset.Now.ToString("o"),
                frame = Time.frameCount,
                activeScene = SceneManager.GetActiveScene().name,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                focusedPath = string.Empty,
                elements = Array.Empty<UiSnapshotElement>(),
                game = Array.Empty<UiSnapshotGameEntry>(),
            };
        }
    }
}
#endif
