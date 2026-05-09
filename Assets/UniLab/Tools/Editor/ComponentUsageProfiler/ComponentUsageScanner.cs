using System;
using System.Collections.Generic;
using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.Tools.Editor.ComponentUsageProfiler
{
    /// <summary>
    /// Scans all build scenes and prefabs to check which MonoBehaviour scripts
    /// in the specified folders are actually placed in the project.
    /// </summary>
    public static class ComponentUsageScanner
    {
        /// <summary>
        /// Checks each MonoBehaviour script under targetFolders to see if it is used in any scene/prefab.
        /// Scans ALL scenes and prefabs regardless of folder — the folder filter only determines
        /// which scripts to check, not which assets to scan.
        /// </summary>
        public static List<ScriptUsageEntry> CheckScriptUsage(List<string> targetFolderRoots)
        {
            // --- Step 1: Collect target MonoBehaviour scripts from specified folders ---
            var targetScripts = CollectTargetScripts(targetFolderRoots);
            if (targetScripts.Count == 0)
            {
                return new List<ScriptUsageEntry>();
            }

            // --- Step 2: Scan ALL scenes and prefabs, collecting used type names ---
            var usedTypeNames = new HashSet<string>();
            // Why: Location tracking per type for used scripts
            var usageLocations = new Dictionary<string, List<ScriptUsageLocation>>();

            try
            {
                ScanAllScenes(usedTypeNames, usageLocations);
                ScanAllPrefabs(usedTypeNames, usageLocations);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // --- Step 3: Mark RequireComponent dependencies as implicitly used ---
            MarkRequireComponentDependencies(usedTypeNames);

            // --- Step 4: Build result entries ---
            var results = new List<ScriptUsageEntry>(targetScripts.Count);
            for (int i = 0; i < targetScripts.Count; i++)
            {
                var target = targetScripts[i];
                var isUsed = usedTypeNames.Contains(target.TypeFullName);

                var entry = new ScriptUsageEntry
                {
                    ScriptPath = target.ScriptPath,
                    TypeFullName = target.TypeFullName,
                    TypeName = target.TypeName,
                    IsUsed = isUsed
                };

                if (isUsed && usageLocations.TryGetValue(target.TypeFullName, out var locations))
                {
                    entry.Locations = locations;
                }

                results.Add(entry);
            }

            return results;
        }

        // --- Target script collection ---

        private struct TargetScript
        {
            public string ScriptPath;
            public string TypeFullName;
            public string TypeName;
        }

        private static List<TargetScript> CollectTargetScripts(List<string> targetFolderRoots)
        {
            var scripts = new List<TargetScript>();
            var scriptGuids = AssetDatabase.FindAssets("t:MonoScript");

            for (int i = 0; i < scriptGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                // Why: folder filter determines which scripts to check
                if (targetFolderRoots.Count > 0 && !ProjectScanFilterUtility.PassFolderFilter(path, targetFolderRoots))
                {
                    continue;
                }

                // Why: skip Editor folder scripts — they are never placed in scenes/prefabs
                if (path.Contains("/Editor/"))
                {
                    continue;
                }

                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript == null)
                {
                    continue;
                }

                var scriptClass = monoScript.GetClass();
                if (scriptClass == null)
                {
                    continue;
                }

                if (!typeof(MonoBehaviour).IsAssignableFrom(scriptClass))
                {
                    continue;
                }

                // Why: abstract and generic types cannot be placed in scenes
                if (scriptClass.IsAbstract || scriptClass.IsGenericTypeDefinition)
                {
                    continue;
                }

                var fullName = scriptClass.FullName;
                if (string.IsNullOrEmpty(fullName))
                {
                    continue;
                }

                // Why: skip Editor assembly types
                var assemblyName = scriptClass.Assembly.GetName().Name;
                if (assemblyName.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                scripts.Add(new TargetScript
                {
                    ScriptPath = path,
                    TypeFullName = fullName,
                    TypeName = scriptClass.Name
                });
            }

            return scripts;
        }

        // --- Scene / Prefab scanning ---

        private static void ScanAllScenes(
            HashSet<string> usedTypeNames,
            Dictionary<string, List<ScriptUsageLocation>> usageLocations)
        {
            var buildScenes = EditorBuildSettings.scenes;
            var scenePaths = new List<string>(buildScenes.Length);
            for (int i = 0; i < buildScenes.Length; i++)
            {
                var scenePath = buildScenes[i].path;
                if (!string.IsNullOrEmpty(scenePath))
                {
                    scenePaths.Add(scenePath);
                }
            }

            SceneScanUtility.ProcessAllScenes(
                scenePaths,
                "Script Usage Check - Scanning Scenes",
                (scene, scenePath) =>
                {
                    var rootGameObjects = scene.GetRootGameObjects();
                    for (int i = 0; i < rootGameObjects.Length; i++)
                    {
                        CollectUsedTypes(rootGameObjects[i], scenePath, usedTypeNames, usageLocations);
                    }
                });
        }

        private static void ScanAllPrefabs(
            HashSet<string> usedTypeNames,
            Dictionary<string, List<ScriptUsageLocation>> usageLocations)
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(prefabPath))
                {
                    continue;
                }

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Script Usage Check - Scanning Prefabs",
                        prefabPath,
                        (float)i / prefabGuids.Length))
                {
                    break;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    continue;
                }

                CollectUsedTypes(prefab, prefabPath, usedTypeNames, usageLocations);
            }
        }

        private static void CollectUsedTypes(
            GameObject gameObject,
            string assetPath,
            HashSet<string> usedTypeNames,
            Dictionary<string, List<ScriptUsageLocation>> usageLocations)
        {
            var components = gameObject.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                var fullName = component.GetType().FullName;
                if (string.IsNullOrEmpty(fullName))
                {
                    continue;
                }

                usedTypeNames.Add(fullName);

                if (!usageLocations.TryGetValue(fullName, out var locations))
                {
                    locations = new List<ScriptUsageLocation>();
                    usageLocations[fullName] = locations;
                }

                locations.Add(new ScriptUsageLocation
                {
                    AssetPath = assetPath,
                    GameObjectPath = ProjectScanEditorUtility.BuildGameObjectPath(component.transform)
                });
            }
        }

        // --- RequireComponent ---

        private static void MarkRequireComponentDependencies(HashSet<string> usedTypeNames)
        {
            var allMonoBehaviourTypes = TypeCache.GetTypesDerivedFrom<MonoBehaviour>();
            var nameToType = new Dictionary<string, Type>(allMonoBehaviourTypes.Count);
            for (int i = 0; i < allMonoBehaviourTypes.Count; i++)
            {
                var type = allMonoBehaviourTypes[i];
                var fullName = type.FullName;
                if (!string.IsNullOrEmpty(fullName))
                {
                    nameToType[fullName] = type;
                }
            }

            var usedSnapshot = new List<string>(usedTypeNames);
            for (int i = 0; i < usedSnapshot.Count; i++)
            {
                if (!nameToType.TryGetValue(usedSnapshot[i], out var type))
                {
                    continue;
                }

                var requireAttributes = type.GetCustomAttributes(typeof(RequireComponent), true);
                for (int j = 0; j < requireAttributes.Length; j++)
                {
                    var requireComponent = (RequireComponent)requireAttributes[j];
                    AddRequiredType(usedTypeNames, requireComponent.m_Type0);
                    AddRequiredType(usedTypeNames, requireComponent.m_Type1);
                    AddRequiredType(usedTypeNames, requireComponent.m_Type2);
                }
            }
        }

        private static void AddRequiredType(HashSet<string> usedTypeNames, Type requiredType)
        {
            if (requiredType == null)
            {
                return;
            }

            var fullName = requiredType.FullName;
            if (!string.IsNullOrEmpty(fullName))
            {
                usedTypeNames.Add(fullName);
            }
        }
    }
}
