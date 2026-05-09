using UniLab.Tools.Editor.ProjectScanCommon;
using UnityEditor;
using UnityEngine;

namespace UniLab.Tools.Editor.HierarchyFavorite
{
    /// <summary>
    /// Adds a context menu item to the GameObject menu for adding Hierarchy objects to favorites.
    /// </summary>
    public static class HierarchyFavoriteContextMenu
    {
        /// <summary>
        /// Adds the currently selected GameObject to Hierarchy favorites.
        /// </summary>
        [MenuItem("GameObject/UniLab/Add to Favorites", false, 49)]
        private static void AddSelectedToFavorite()
        {
            var gameObject = Selection.activeGameObject;
            if (gameObject == null)
            {
                return;
            }

            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);

            if (globalId.identifierType == 0)
            {
                EditorUtility.DisplayDialog(
                    EditorToolLabels.Get(LabelKey.HierarchyFavoriteTitle),
                    EditorToolLabels.Get(LabelKey.SceneNotSavedMessage),
                    "OK");
                return;
            }

            var entry = new FavoriteEntry
            {
                GlobalObjectIdString = globalId.ToString(),
                GameObjectName = gameObject.name,
                GameObjectPath = BuildGameObjectPath(gameObject.transform),
                ScenePath = gameObject.scene.path,
                SceneName = gameObject.scene.name,
                Memo = string.Empty
            };

            if (HierarchyFavoriteWindow.Instance != null)
            {
                HierarchyFavoriteWindow.Instance.AddFavoriteEntry(entry);
            }
            else
            {
                HierarchyFavoriteData.AddEntry(entry);
            }
        }

        private static string BuildGameObjectPath(Transform target)
        {
            return ProjectScanEditorUtility.BuildGameObjectPath(target);
        }

        [MenuItem("GameObject/UniLab/Add to Favorites", true)]
        private static bool ValidateAddSelectedToFavorite()
        {
            return Selection.activeGameObject != null;
        }
    }
}
