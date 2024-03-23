using UnityEditor;
using UnityEngine;

namespace UniLab.Editor.UI
{
    public static class UniLabEditor
    {
        private const string _uiPath = "UniLab/UI/";

        private const string _uniButton = "UniButton";

        [MenuItem("GameObject/UniLab/UI/" + _uniButton, false, 100)]
        private static void CreateUniButton()
        {
            CreatePrefab(_uiPath + _uniButton);
        }

        private static void CreatePrefab(string path)
        {
            var prefab = Resources.Load(path);
            var instance = Object.Instantiate(prefab, Selection.activeTransform);
            instance.name = _uniButton;
        }
    }
}