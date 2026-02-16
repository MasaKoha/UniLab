#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UniLab.UI.Editor
{
    public static class UIComponentTemplateMenu
    {
        private const string SettingsResourcePath = "UniLabEditor/TemplatePrefabs";
        private const string VariableGridMenuPath = "GameObject/UniLab/UI/Variable Grid Layout Group";

        [MenuItem( VariableGridMenuPath, false, -10 )]
        private static void CreateVariableGridTemplateInstance( MenuCommand command )
        {
            var prefabs = Resources.Load<TemplatePrefabs>( SettingsResourcePath );
            var instance = CreateInstanceFromPrefab( prefabs.VariableGridLayoutGroupPrefab );
            ApplyParentAndSelection( command, instance );
        }

        private static GameObject CreateInstanceFromPrefab( GameObject prefab )
        {
            return PrefabUtility.InstantiatePrefab( prefab ) as GameObject;
        }

        private static void ApplyParentAndSelection( MenuCommand command, GameObject instance )
        {
            var parent = command.context as GameObject;
            if (parent == null)
            {
                parent = Selection.activeGameObject;
            }

            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign( instance, parent );
            }

            PrefabUtility.UnpackPrefabInstance( instance, PrefabUnpackMode.Completely, InteractionMode.UserAction );
            Selection.activeGameObject = instance;
        }
    }
}
#endif
