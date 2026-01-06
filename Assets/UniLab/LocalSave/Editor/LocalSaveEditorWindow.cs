using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniLab.LocalSave.Editor
{
    public sealed class LocalSaveEditorWindow : EditorWindow
    {
        private Vector2 _scroll;
        private List<string> _keys;

        [MenuItem("UniLab/LocalSave/SaveDataManage")]
        public static void Open()
        {
            GetWindow<LocalSaveEditorWindow>("SaveDataManage");
        }

        private void OnEnable()
        {
            RefreshKeys();
        }

        private void RefreshKeys()
        {
            _keys = LocalSave.GetAllKeysInEditor();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Refresh"))
            {
                RefreshKeys();
            }

            if (GUILayout.Button("Delete All"))
            {
                if (EditorUtility.DisplayDialog("Confirm", "全てのローカルのセーブデータを削除します。よろしいですか？", "はい", "キャンセル"))
                {
                    LocalSave.DeleteAll();
                    RefreshKeys();
                }
            }

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var key in _keys.ToArray())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(key);
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    LocalSave.DeleteEditorOnly(key);
                    RefreshKeys();
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}