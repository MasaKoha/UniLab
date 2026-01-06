#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MessagePack;
using UniLab.Common.Utility;
using UnityEditor;
using UnityEngine;

namespace UniLab.Feature.MasterData.Editor
{
    /// <summary>
    /// ローカルに保存された暗号化マスターを aes_key.txt の鍵で復号して閲覧する簡易ビューア。
    /// </summary>
    public sealed class MasterLocalViewer : EditorWindow
    {
        private readonly List<MasterEntry> _entries = new();
        private Type[] _managerTypes = Array.Empty<Type>();
        private string[] _managerTypeNames = Array.Empty<string>();
        private int _selectedManagerIndex;
        private Vector2 _scroll;
        private byte[] _key;
        private byte[] _iv;
        private string _status = "Click Refresh to load local masters.";

        [MenuItem("UniLab/MasterData/View Local Masters")]
        private static void Open()
        {
            GetWindow<MasterLocalViewer>("Local Master Viewer");
        }

        private void OnEnable()
        {
            RefreshManagerTypes();
            Refresh();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_managerTypes.Length == 0)
                {
                    EditorGUILayout.LabelField("MasterManager not found.", EditorStyles.boldLabel);
                }
                else
                {
                    var newIndex = EditorGUILayout.Popup(_selectedManagerIndex, _managerTypeNames, GUILayout.Width(220));
                    if (newIndex != _selectedManagerIndex)
                    {
                        _selectedManagerIndex = newIndex;
                        Refresh();
                    }
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                {
                    RefreshManagerTypes();
                    Refresh();
                }

                EditorGUILayout.LabelField(_status, EditorStyles.boldLabel);
            }

            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;
            foreach (var entry in _entries)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(entry.MasterId ?? "(unknown)", GUILayout.Width(200)))
                        {
                            MasterGridWindow.Open(entry, _key, _iv);
                        }

                        if (GUILayout.Button(entry.Path, EditorStyles.linkLabel))
                        {
                            EditorUtility.RevealInFinder(entry.Path);
                        }
                    }
                }
            }
        }

        private void Refresh()
        {
            if (_managerTypes.Length == 0)
            {
                _status = "MasterManager not found in loaded assemblies.";
                return;
            }

            _entries.Clear();
            var managerType = _managerTypes[Mathf.Clamp(_selectedManagerIndex, 0, _managerTypes.Length - 1)];
            var adapter = MasterManagerAdapter.Create(managerType);
            if (adapter == null)
            {
                _status = $"Failed to access manager: {managerType.Name}";
                return;
            }

            adapter.LoadAesKey(out _key, out _iv);

            if (_key == null || _iv == null)
            {
                _status = "aes_key.txt が見つかりませんでした。マスター出力時に鍵を保存してください。";
                return;
            }

            var dir = adapter.SavePath;
            if (string.IsNullOrWhiteSpace(dir))
            {
                _status = "SavePath が空です。";
                return;
            }

            if (!Directory.Exists(dir))
            {
                _status = $"SavePath が存在しません: {dir}";
                return;
            }

            var files = Directory.GetFiles(dir, "*.master");
            foreach (var file in files)
            {
                var id = DecodeBase64(Path.GetFileNameWithoutExtension(file));
                _entries.Add(new MasterEntry { Path = file, MasterId = id });
            }

            _status = $"Found {_entries.Count} master files in {dir}";
        }

        private static string DecodeBase64(string base64)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            }
            catch
            {
                return base64;
            }
        }

        private void RefreshManagerTypes()
        {
            _managerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(IsMasterManagerType)
                .OrderBy(t => t.Name)
                .ToArray();

            _managerTypeNames = _managerTypes.Select(t => t.Name).ToArray();
            _selectedManagerIndex = Mathf.Clamp(_selectedManagerIndex, 0, Mathf.Max(0, _managerTypes.Length - 1));
        }

        private static bool IsMasterManagerType(Type type)
        {
            if (type == null || !type.IsClass || type.IsAbstract)
            {
                return false;
            }

            return InheritsGeneric(type, typeof(MasterManager<>));
        }

        private static bool InheritsGeneric(Type type, Type genericBase)
        {
            while (type != null && type != typeof(object))
            {
                var current = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (current == genericBase)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

    }

    internal sealed class MasterEntry
    {
        public string Path;
        public string MasterId;
    }

    internal sealed class MasterManagerAdapter
    {
        private readonly object _instance;
        private readonly MethodInfo _loadAesKey;
        private readonly PropertyInfo _savePath;

        private MasterManagerAdapter(object instance, MethodInfo loadAesKey, PropertyInfo savePath)
        {
            _instance = instance;
            _loadAesKey = loadAesKey;
            _savePath = savePath;
        }

        public string SavePath => _savePath?.GetValue(_instance) as string;

        public void LoadAesKey(out byte[] key, out byte[] iv)
        {
            if (_loadAesKey == null)
            {
                key = null;
                iv = null;
                return;
            }

            var args = new object[] { null, null };
            _loadAesKey.Invoke(_instance, args);
            key = args[0] as byte[];
            iv = args[1] as byte[];
        }

        public static MasterManagerAdapter Create(Type managerType)
        {
            if (managerType == null)
            {
                return null;
            }

            object instance = null;
            try
            {
                var instanceProp = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                instance = instanceProp?.GetValue(null);
            }
            catch
            {
                // ignore and fallback
            }

            if (instance == null)
            {
                try
                {
                    instance = Activator.CreateInstance(managerType);
                }
                catch
                {
                    instance = null;
                }
            }

            if (instance == null)
            {
                return null;
            }

            var loadAesKey = managerType.GetMethod("LoadAesKey", BindingFlags.Public | BindingFlags.Instance);
            var savePath = managerType.GetProperty("SavePath", BindingFlags.Public | BindingFlags.Instance);
            return new MasterManagerAdapter(instance, loadAesKey, savePath);
        }
    }

    internal sealed class MasterGridWindow : EditorWindow
    {
        private MasterEntry _entry;
        private byte[] _key;
        private byte[] _iv;
        private object[] _records;
        private FieldInfo[] _fields;
        private float[] _columnWidths;
        private int _dragColumn = -1;
        private float _dragStartX;
        private float _dragStartWidth;
        private const float CellHeight = 22f;
        private string _status = "Loading...";
        private Vector2 _scroll;

        public static void Open(MasterEntry entry, byte[] key, byte[] iv)
        {
            var wnd = CreateInstance<MasterGridWindow>();
            wnd.titleContent = new GUIContent($"{entry.MasterId} Records");
            wnd._entry = entry;
            wnd._key = key;
            wnd._iv = iv;
            wnd.Load();
            wnd.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(_status, EditorStyles.boldLabel);
            if (_records == null || _fields == null || _fields.Length == 0)
            {
                return;
            }

            var evt = Event.current;
            HandleDrag(evt);

            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;

            DrawHeader(evt);

            foreach (var record in _records)
            {
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    for (var i = 0; i < _fields.Length; i++)
                    {
                        var value = _fields[i].GetValue(record);
                        var rect = GUILayoutUtility.GetRect(_columnWidths[i], CellHeight, GUILayout.Width(_columnWidths[i]));
                        DrawCell(rect, value?.ToString() ?? "-");
                    }
                }
            }
        }

        private void Load()
        {
            try
            {
                var encrypted = File.ReadAllBytes(_entry.Path);
                var decrypted = AesEncryptionUtility.Decrypt(encrypted, _key, _iv);

                var masterType = FindMasterType(_entry.MasterId);
                if (masterType == null)
                {
                    _status = $"Type not found: {_entry.MasterId}";
                    return;
                }

                var master = MessagePackSerializer.Deserialize(masterType, decrypted) as MasterBase;
                if (master == null)
                {
                    _status = "Failed to deserialize master.";
                    return;
                }

                var recordsField = masterType.GetField("Records", BindingFlags.Instance | BindingFlags.Public);
                var enumerable = recordsField?.GetValue(master) as IEnumerable;
                if (enumerable == null)
                {
                    _status = "No records field or empty.";
                    return;
                }

                _records = enumerable.Cast<object>().ToArray();
                _fields = _records.FirstOrDefault()?.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
                if (_fields == null || _fields.Length == 0)
                {
                    _status = "Record type has no public fields.";
                    return;
                }

                _columnWidths = CalculateColumnWidths(_records, _fields);
                _status = $"Loaded {_records.Length} records.";
            }
            catch (Exception e)
            {
                _status = $"Failed to load: {e.Message}";
                Debug.LogError($"MasterGridWindow load failed for {_entry.Path}: {e}");
            }
        }

        private static Type FindMasterType(string masterId)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .FirstOrDefault(t => string.Equals(t.Name, masterId, StringComparison.Ordinal));
        }

        private void DrawHeader(Event evt)
        {
            if (_fields == null || _columnWidths == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                for (var i = 0; i < _fields.Length; i++)
                {
                    var rect = GUILayoutUtility.GetRect(_columnWidths[i], CellHeight, GUILayout.Width(_columnWidths[i]));
                    DrawCell(rect, _fields[i].Name, EditorStyles.miniBoldLabel, true);

                    if (i < _fields.Length - 1)
                    {
                        var handleRect = new Rect(rect.xMax - 2f, rect.y, 4f, rect.height + 6f);
                        EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);
                        if (evt.type == EventType.MouseDown && handleRect.Contains(evt.mousePosition))
                        {
                            _dragColumn = i;
                            _dragStartX = evt.mousePosition.x;
                            _dragStartWidth = _columnWidths[i];
                            evt.Use();
                        }
                    }
                }
            }
        }

        private static float[] CalculateColumnWidths(IReadOnlyList<object> records, IReadOnlyList<FieldInfo> fields)
        {
            var widths = new float[fields.Count];
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var maxText = field.Name;
                foreach (var record in records)
                {
                    var value = field.GetValue(record);
                    var text = value?.ToString() ?? "-";
                    if (text.Length > maxText.Length)
                    {
                        maxText = text;
                    }
                }

                var size = EditorStyles.label.CalcSize(new GUIContent(maxText));
                widths[i] = Mathf.Clamp(size.x + 16f, 80f, 800f);
            }

            return widths;
        }

        private void HandleDrag(Event evt)
        {
            if (_dragColumn < 0 || _columnWidths == null)
            {
                return;
            }

            const float minWidth = 60f;
            const float maxWidth = 800f;

            if (evt.type == EventType.MouseDrag)
            {
                var delta = evt.mousePosition.x - _dragStartX;
                _columnWidths[_dragColumn] = Mathf.Clamp(_dragStartWidth + delta, minWidth, maxWidth);
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp)
            {
                _dragColumn = -1;
            }
        }

        private static void DrawCell(Rect rect, string text, GUIStyle style = null, bool isHeader = false)
        {
            var bg = isHeader
                ? (EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f))
                : (EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f, 1f) : new Color(0.96f, 0.96f, 0.96f, 1f));
            EditorGUI.DrawRect(rect, bg);

            // borders
            var borderColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), borderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), borderColor);

            var contentRect = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, rect.height - 4f);
            GUI.Label(contentRect, text, style ?? EditorStyles.label);
        }
    }
}
#endif
