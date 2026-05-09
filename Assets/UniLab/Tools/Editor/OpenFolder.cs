using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace UniLab.Tools.Editor
{
    /// <summary>
    /// Provides menu commands to reveal platform-specific folders in the file browser.
    /// </summary>
    public static class OpenFolder
    {
        /// <summary>
        /// Opens the platform-specific PlayerPrefs folder in the file browser.
        /// </summary>
        [MenuItem("UniLab/Tools/Open Folder/Open PlayerPrefs")]
        public static void OpenPlayerPrefsFolder()
        {
#if UNITY_EDITOR_OSX
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            var path = Path.Combine(home, "Library/Preferences");
            EditorUtility.RevealInFinder(path);
#elif UNITY_EDITOR_WIN
            var path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Unity");
            EditorUtility.RevealInFinder(path);
#else
            Debug.LogWarning(EditorToolLabels.Get(LabelKey.UnsupportedPlatform));
#endif
        }

        /// <summary>
        /// Opens Application.persistentDataPath in the file browser.
        /// </summary>
        [MenuItem("UniLab/Tools/Open Folder/PersistentDataPath")]
        public static void OpenPersistentDataPath()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}