using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.ProjectScanCommon
{
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

                if (HasMissingReferences(new SerializedObject(component)))
                {
                    return true;
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
            return HasMissingReferences(new SerializedObject(obj));
        }
    }
}
