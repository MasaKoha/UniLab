using UnityEditor;
using UnityEngine;

namespace UniLab.Localization.Editor
{
#if UNITY_EDITOR
    public static class LocalizedTextUpdater
    {
        public static void UpdateAllLocalizedTexts()
        {
            var texts = Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Exclude);
            foreach (var text in texts)
            {
                var method = typeof(LocalizedText).GetMethod("UpdateText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method.Invoke(text, null);
                EditorUtility.SetDirty(text);
            }

            Debug.Log("All LocalizedText updated.");
        }
    }
#endif
}
