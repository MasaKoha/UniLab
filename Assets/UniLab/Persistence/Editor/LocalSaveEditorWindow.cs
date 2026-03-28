using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniLab.Persistence.Editor
{
    /// <summary>
    /// Editor window for managing LocalSave keys and PlayerPrefs data.
    /// </summary>
    public sealed class LocalSaveEditorWindow : EditorWindow
    {
        private const float StatusLabelWidth = 70f;
        private const float DeleteButtonWidth = 60f;
        private const float RowSpacing = 2f;
        private static readonly Vector2 MinWindowSize = new Vector2(480f, 600f);

        private List<string> _keys = new List<string>();
        private Vector2 _scrollPosition;
        private string _directDeleteKey = string.Empty;

        /// <summary>Opens the LocalSave Manager window.</summary>
        [MenuItem("UniLab/LocalSave/SaveDataManage")]
        public static void Open()
        {
            GetWindow<LocalSaveEditorWindow>("LocalSave Manager");
        }

        private void OnEnable()
        {
            minSize = MinWindowSize;
            RefreshKeys();
        }

        private void RefreshKeys()
        {
            _keys = LocalSave.GetAllKeysInEditor();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Manages registered LocalSave keys and allows direct PlayerPrefs deletion.", MessageType.Info);
            EditorGUILayout.Space();

            DrawDeleteAllSection();
            EditorGUILayout.Space();

            DrawDirectKeyDeletionSection();
            EditorGUILayout.Space();

            DrawRegisteredKeysSection();
        }

        private void DrawDeleteAllSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Delete All", EditorStyles.boldLabel);
                if (GUILayout.Button("Delete All LocalSave Data"))
                {
                    if (EditorUtility.DisplayDialog("Confirm", "Delete all PlayerPrefs data? This clears all LocalSave entries and any other PlayerPrefs keys.", "Delete", "Cancel"))
                    {
                        LocalSave.DeleteAll();
                        RefreshKeys();
                    }
                }
            }
        }

        private void DrawDirectKeyDeletionSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Direct Key Deletion", EditorStyles.boldLabel);
                _directDeleteKey = EditorGUILayout.TextField("Key", _directDeleteKey);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_directDeleteKey)))
                {
                    if (GUILayout.Button("Delete Key"))
                    {
                        if (EditorUtility.DisplayDialog("Confirm", $"Delete PlayerPrefs key \"{_directDeleteKey}\"?", "Delete", "Cancel"))
                        {
                            PlayerPrefs.DeleteKey(_directDeleteKey);
                            PlayerPrefs.Save();
                            RefreshKeys();
                            _directDeleteKey = string.Empty;
                            Repaint();
                        }
                    }
                }
            }
        }

        private void DrawRegisteredKeysSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Registered Keys", EditorStyles.boldLabel);
                    if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
                    {
                        RefreshKeys();
                    }
                }

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                foreach (var key in _keys.ToArray())
                {
                    DrawKeyRow(key);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawKeyRow(string key)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var rowRect = EditorGUILayout.GetControlRect(false, lineHeight + RowSpacing);

            var keyRect = new Rect(rowRect.x, rowRect.y, rowRect.width - StatusLabelWidth - DeleteButtonWidth - 8f, lineHeight);
            var statusRect = new Rect(keyRect.xMax + 4f, rowRect.y, StatusLabelWidth, lineHeight);
            var deleteRect = new Rect(statusRect.xMax + 4f, rowRect.y, DeleteButtonWidth, lineHeight);

            EditorGUI.SelectableLabel(keyRect, key);

            var isSaved = PlayerPrefs.HasKey(key);
            EditorGUI.LabelField(statusRect, isSaved ? "Saved" : "Not saved");

            using (new EditorGUI.DisabledScope(!isSaved))
            {
                if (GUI.Button(deleteRect, "Delete"))
                {
                    if (EditorUtility.DisplayDialog("Confirm", $"Delete key \"{key}\"?", "Delete", "Cancel"))
                    {
                        LocalSave.DeleteEditorOnly(key);
                        RefreshKeys();
                    }
                }
            }
        }
    }
}
