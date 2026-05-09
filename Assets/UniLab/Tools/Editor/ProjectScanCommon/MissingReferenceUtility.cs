using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.ProjectScanCommon
{
    /// <summary>
    /// Holds the component type name and property path of a single missing reference field.
    /// </summary>
    public readonly struct MissingFieldInfo
    {
        /// <summary>
        /// The fully-qualified type name of the component (or "(Missing Script)" for null components).
        /// </summary>
        public readonly string ComponentTypeName;

        /// <summary>
        /// The SerializedProperty path of the missing reference (empty for Missing Script entries).
        /// </summary>
        public readonly string PropertyPath;

        public MissingFieldInfo(string componentTypeName, string propertyPath)
        {
            ComponentTypeName = componentTypeName;
            PropertyPath = propertyPath;
        }
    }

    /// <summary>
    /// Detects missing serialized references on GameObjects and arbitrary UnityEngine.Objects.
    /// </summary>
    public static class MissingReferenceUtility
    {
        /// <summary>
        /// Returns true if any component on the GameObject is missing or has a missing serialized reference.
        /// </summary>
        public static bool HasMissingReferences(GameObject gameObject)
        {
            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    return true;
                }

                using (var serializedObject = new SerializedObject(component))
                {
                    if (HasMissingReferences(serializedObject))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if any visible ObjectReference property in the SerializedObject points to a missing asset.
        /// </summary>
        public static bool HasMissingReferences(SerializedObject serializedObject)
        {
            var iterator = serializedObject.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

#if UNITY_6000_4_OR_NEWER
                if (iterator.objectReferenceValue == null && iterator.objectReferenceEntityIdValue != default)
#else
                if (iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
#endif
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if any visible ObjectReference property on the Object points to a missing asset.
        /// </summary>
        public static bool HasMissingReferences(Object obj)
        {
            using (var serializedObject = new SerializedObject(obj))
            {
                return HasMissingReferences(serializedObject);
            }
        }

        /// <summary>
        /// Collects all missing reference fields on a GameObject (including children).
        /// Returns an empty list when no missing references are found.
        /// </summary>
        public static List<MissingFieldInfo> CollectMissingFields(GameObject gameObject)
        {
            var results = new List<MissingFieldInfo>();
            CollectMissingFieldsRecursive(gameObject, results);
            return results;
        }

        /// <summary>
        /// Collects all missing reference fields on a single UnityEngine.Object (non-GameObject).
        /// Returns an empty list when no missing references are found.
        /// </summary>
        public static List<MissingFieldInfo> CollectMissingFields(Object obj)
        {
            var results = new List<MissingFieldInfo>();
            using (var serializedObject = new SerializedObject(obj))
            {
                CollectMissingFieldsFromSerializedObject(serializedObject, obj.GetType().Name, results);
            }

            return results;
        }

        private static void CollectMissingFieldsRecursive(GameObject gameObject, List<MissingFieldInfo> results)
        {
            var components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    results.Add(new MissingFieldInfo("(Missing Script)", ""));
                    continue;
                }

                using (var serializedObject = new SerializedObject(components[i]))
                {
                    CollectMissingFieldsFromSerializedObject(
                        serializedObject,
                        components[i].GetType().Name,
                        results);
                }
            }

            var transform = gameObject.transform;
            for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                CollectMissingFieldsRecursive(transform.GetChild(childIndex).gameObject, results);
            }
        }

        private static void CollectMissingFieldsFromSerializedObject(
            SerializedObject serializedObject,
            string componentTypeName,
            List<MissingFieldInfo> results)
        {
            var iterator = serializedObject.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

#if UNITY_6000_4_OR_NEWER
                if (iterator.objectReferenceValue == null && iterator.objectReferenceEntityIdValue != default)
#else
                if (iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
#endif
                {
                    results.Add(new MissingFieldInfo(componentTypeName, iterator.propertyPath));
                }
            }
        }
    }
}
