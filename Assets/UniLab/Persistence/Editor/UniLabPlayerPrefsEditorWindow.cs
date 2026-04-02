using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UniLab.Persistence.Editor
{
    /// <summary>
    /// Editor window for managing PlayerPrefs and browsing known UniLab PlayerPrefs keys.
    /// </summary>
    public sealed class UniLabPlayerPrefsEditorWindow : EditorWindow
    {
        private const float RefreshButtonWidth = 70f;
        private const float StatusLabelWidth = 60f;
        private const float DeleteButtonWidth = 60f;
        private static readonly Vector2 MinWindowSize = new Vector2(480f, 600f);
        private static readonly string[] ExcludedAssemblyPrefixes =
        {
            "UnityEngine",
            "UnityEditor",
            "Unity.",
            "System",
            "mscorlib",
            "Mono.",
            "netstandard",
        };

        private static List<string> _cachedKeys = new List<string>();
        private static bool _isCacheValid;

        private Vector2 _scrollPosition;
        private string _targetKey = string.Empty;

        /// <summary>
        /// Opens the UniLab PlayerPrefs management window.
        /// </summary>
        [MenuItem("UniLab/PlayerPrefs/管理")]
        public static void ShowWindow()
        {
            GetWindow<UniLabPlayerPrefsEditorWindow>("UniLab PlayerPrefs");
        }

        [InitializeOnLoadMethod]
        private static void RegisterAssemblyReloadEvents()
        {
            AssemblyReloadEvents.afterAssemblyReload -= InvalidateCache;
            AssemblyReloadEvents.afterAssemblyReload += InvalidateCache;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("UniLab PlayerPrefs");
            minSize = MinWindowSize;

            InvalidateCache();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("保存済みのPlayerPrefsを管理します。", MessageType.Info);
            EditorGUILayout.Space();

            DrawDeleteAllSection();
            EditorGUILayout.Space();

            DrawDeleteByKeySection();
            EditorGUILayout.Space();

            DrawKnownKeysSection();
        }

        private void DrawDeleteAllSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("全削除", EditorStyles.boldLabel);

                if (GUILayout.Button("PlayerPrefs をすべて削除"))
                {
                    if (!EditorUtility.DisplayDialog("確認", "PlayerPrefs をすべて削除します。よろしいですか？", "削除", "キャンセル"))
                    {
                        return;
                    }

                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                    Repaint();
                }
            }
        }

        private void DrawDeleteByKeySection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("キー指定削除", EditorStyles.boldLabel);
                _targetKey = EditorGUILayout.TextField("Key", _targetKey);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_targetKey)))
                {
                    if (GUILayout.Button("入力した Key を削除"))
                    {
                        if (!EditorUtility.DisplayDialog("確認", $"PlayerPrefs のキー \"{_targetKey}\" を削除します。よろしいですか？", "削除", "キャンセル"))
                        {
                            return;
                        }

                        PlayerPrefs.DeleteKey(_targetKey);
                        PlayerPrefs.Save();
                        Repaint();
                    }
                }
            }
        }

        private void DrawKnownKeysSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("既知のキー", EditorStyles.boldLabel);

                    if (GUILayout.Button("再読込", GUILayout.Width(RefreshButtonWidth)))
                    {
                        InvalidateCache();
                    }
                }

                var keys = GetKeys();
                if (keys.Count == 0)
                {
                    EditorGUILayout.LabelField("キーは見つかりませんでした。");
                    return;
                }

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                foreach (var key in keys)
                {
                    DrawKnownKeyRow(key);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawKnownKeyRow(string key)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(key, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                GUILayout.Label(PlayerPrefs.HasKey(key) ? "保存済み" : "未保存", GUILayout.Width(StatusLabelWidth));

                using (new EditorGUI.DisabledScope(!PlayerPrefs.HasKey(key)))
                {
                    if (GUILayout.Button("削除", GUILayout.Width(DeleteButtonWidth)))
                    {
                        if (!EditorUtility.DisplayDialog("確認", $"PlayerPrefs のキー \"{key}\" を削除します。よろしいですか？", "削除", "キャンセル"))
                        {
                            return;
                        }

                        PlayerPrefs.DeleteKey(key);
                        PlayerPrefs.Save();
                        Repaint();
                    }
                }
            }
        }

        private static List<string> GetKeys()
        {
            if (_isCacheValid)
            {
                return _cachedKeys;
            }

            _cachedKeys = ScanKeys();
            _isCacheValid = true;
            return _cachedKeys;
        }

        private static List<string> ScanKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName))
                {
                    continue;
                }

                if (IsExcludedAssembly(assemblyName))
                {
                    continue;
                }

                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (!type.IsDefined(typeof(UniLabPlayerPrefsKeySourceAttribute), false))
                    {
                        continue;
                    }

                    foreach (var key in CollectKeysFromType(type))
                    {
                        keys.Add(key);
                    }
                }
            }

            var scannedKeys = new List<string>(keys);
            scannedKeys.Sort(StringComparer.Ordinal);
            return scannedKeys;
        }

        private static void InvalidateCache()
        {
            _cachedKeys = new List<string>();
            _isCacheValid = false;
        }

        private static bool IsExcludedAssembly(string assemblyName)
        {
            foreach (var excludedAssemblyPrefix in ExcludedAssemblyPrefixes)
            {
                if (assemblyName.StartsWith(excludedAssemblyPrefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                var loadableTypes = new List<Type>();
                foreach (var type in exception.Types)
                {
                    if (type == null)
                    {
                        continue;
                    }

                    loadableTypes.Add(type);
                }

                return loadableTypes;
            }
        }

        private static IEnumerable<string> CollectKeysFromType(Type type)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                if (!field.IsLiteral || field.IsInitOnly)
                {
                    continue;
                }

                var key = field.GetRawConstantValue() as string;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                yield return key;
            }
        }
    }
}
