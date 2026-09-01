using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.Diagnostics
{
    /// <summary>
    /// ロード済みシーンの階層構造を JSON 化する。
    /// </summary>
    public static class SceneHierarchyDumper
    {
        private const int WorldCornerCount = 4;
        private const int TextPreviewMaxLength = 40;
        private const int RootParentIndex = -1;

        /// <summary>
        /// ロード済み全シーンの階層をダンプする。
        /// </summary>
        public static SceneHierarchyDump Dump()
        {
            Canvas.ForceUpdateCanvases();

            var scenes = new List<SceneHierarchyScene>();
            var sceneCount = SceneManager.sceneCount;
            for (var sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var nodes = new List<SceneHierarchyNode>();
                var nextNodeIndex = 0;
                var rootGameObjects = scene.GetRootGameObjects();
                for (var rootIndex = 0; rootIndex < rootGameObjects.Length; rootIndex++)
                {
                    AppendNodeRecursive(rootGameObjects[rootIndex].transform, RootParentIndex, rootGameObjects[rootIndex].name, nodes, ref nextNodeIndex);
                }

                scenes.Add(new SceneHierarchyScene
                {
                    name = scene.name,
                    nodes = nodes.ToArray(),
                });
            }

            return new SceneHierarchyDump
            {
                capturedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                scenes = scenes.ToArray(),
            };
        }

        private static void AppendNodeRecursive(Transform transform, int parentIndex, string path, List<SceneHierarchyNode> nodes, ref int nextNodeIndex)
        {
            var currentIndex = nextNodeIndex;
            nextNodeIndex++;

            var node = BuildNode(transform, currentIndex, parentIndex, path);
            nodes.Add(node);

            var childCount = transform.childCount;
            for (var childIndex = 0; childIndex < childCount; childIndex++)
            {
                var childTransform = transform.GetChild(childIndex);
                var childPath = $"{path}/{childTransform.name}";
                AppendNodeRecursive(childTransform, currentIndex, childPath, nodes, ref nextNodeIndex);
            }
        }

        private static SceneHierarchyNode BuildNode(Transform transform, int index, int parentIndex, string path)
        {
            var components = transform.GetComponents<Component>();
            var componentTypeNames = new string[components.Length];
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                var component = components[componentIndex];
                componentTypeNames[componentIndex] = component == null ? "MissingComponent" : component.GetType().Name;
            }

            var serializedFields = CollectSerializedFieldWirings(components);
            var node = new SceneHierarchyNode
            {
                index = index,
                parentIndex = parentIndex,
                path = path,
                name = transform.name,
                activeSelf = transform.gameObject.activeSelf,
                componentTypeNames = componentTypeNames,
                serializedFields = serializedFields.ToArray(),
                hasRectTransform = false,
                hasTextMeshPro = false,
            };

            if (transform is RectTransform rectTransform)
            {
                node.hasRectTransform = true;
                node.anchorMin = ToFloatArray(rectTransform.anchorMin);
                node.anchorMax = ToFloatArray(rectTransform.anchorMax);
                node.pivot = ToFloatArray(rectTransform.pivot);
                node.anchoredPosition = ToFloatArray(rectTransform.anchoredPosition);
                node.sizeDelta = ToFloatArray(rectTransform.sizeDelta);
                node.worldRect = GetWorldRectValues(rectTransform);
            }

            if (transform.TryGetComponent<TextMeshProUGUI>(out var textMeshPro))
            {
                node.hasTextMeshPro = true;
                node.text = BuildTextPreview(textMeshPro.text);
                node.fontSize = textMeshPro.fontSize;
                node.textWrappingMode = textMeshPro.textWrappingMode.ToString();
                node.overflowMode = textMeshPro.overflowMode.ToString();
            }

            return node;
        }

        private static List<SerializedFieldWiring> CollectSerializedFieldWirings(Component[] components)
        {
            var wirings = new List<SerializedFieldWiring>();
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                var component = components[componentIndex];
                if (component == null)
                {
                    continue;
                }

                if (component is not MonoBehaviour monoBehaviour)
                {
                    continue;
                }

                var componentType = monoBehaviour.GetType();
                var fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    var fieldInfo = fields[fieldIndex];
                    if (!IsSerializableObjectReferenceField(fieldInfo))
                    {
                        continue;
                    }

                    var value = fieldInfo.GetValue(monoBehaviour) as UnityEngine.Object;
                    var isNull = value == null;
                    wirings.Add(new SerializedFieldWiring
                    {
                        componentTypeName = componentType.Name,
                        fieldName = fieldInfo.Name,
                        isNull = isNull,
                    });
                }
            }

            return wirings;
        }

        private static bool IsSerializableObjectReferenceField(FieldInfo fieldInfo)
        {
            if (!typeof(UnityEngine.Object).IsAssignableFrom(fieldInfo.FieldType))
            {
                return false;
            }

            if (fieldInfo.IsPublic)
            {
                return true;
            }

            return Attribute.IsDefined(fieldInfo, typeof(SerializeField));
        }

        private static float[] ToFloatArray(Vector2 vector)
        {
            return new[]
            {
                vector.x,
                vector.y,
            };
        }

        private static float[] GetWorldRectValues(RectTransform rectTransform)
        {
            var corners = new Vector3[WorldCornerCount];
            rectTransform.GetWorldCorners(corners);
            var minX = corners[0].x;
            var maxX = corners[0].x;
            var minY = corners[0].y;
            var maxY = corners[0].y;
            for (var cornerIndex = 1; cornerIndex < corners.Length; cornerIndex++)
            {
                var corner = corners[cornerIndex];
                if (corner.x < minX)
                {
                    minX = corner.x;
                }

                if (corner.x > maxX)
                {
                    maxX = corner.x;
                }

                if (corner.y < minY)
                {
                    minY = corner.y;
                }

                if (corner.y > maxY)
                {
                    maxY = corner.y;
                }
            }

            return new[]
            {
                minX,
                minY,
                maxX - minX,
                maxY - minY,
            };
        }

        private static string BuildTextPreview(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\\n");
            if (normalizedText.Length <= TextPreviewMaxLength)
            {
                return normalizedText;
            }

            return normalizedText.Substring(0, TextPreviewMaxLength);
        }
    }
}
#endif
