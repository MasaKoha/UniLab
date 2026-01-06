using UnityEditor;
using UnityEngine;
using System.IO;

namespace UniLab.Tools.Editor
{
    public static class OpenFolder
    {
        [MenuItem("UniLab/Open Folder/Open PlayerPrefs")]
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
            Debug.LogWarning("このOSには対応していません");
#endif
        }

        [MenuItem("UniLab/Open Folder/PersistentDataPath")]
        public static void OpenPersistentDataPath()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}