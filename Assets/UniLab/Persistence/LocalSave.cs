using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UniLab.Persistence
{
    public static class LocalSave
    {
        private static string GetKeyName<TData>() => typeof(TData).FullName;

        public static void Save<TData>(TData data)
        {
            var json = JsonUtility.ToJson(data);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            var key = GetKeyName<TData>();
            PlayerPrefs.SetString(key, base64);
            PlayerPrefs.Save();
#if UNITY_EDITOR
            RegisterKeyInEditor(key);
#endif
        }

        public static TData Load<TData>() where TData : new()
        {
            var key = GetKeyName<TData>();
            if (!PlayerPrefs.HasKey(key))
            {
                return new TData();
            }

            var base64 = PlayerPrefs.GetString(key);
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return JsonUtility.FromJson<TData>(json);
        }

        public static void Delete<T>()
        {
            var key = GetKeyName<T>();
            PlayerPrefs.DeleteKey(key);
#if UNITY_EDITOR
            RegisterKeyInEditor(key);
#endif
        }

        public static void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR // Editor-only implementation for viewing and deleting specific save data entries.
        private const string KeyListKey = "KeyList";

        public static List<string> GetAllKeysInEditor()
        {
            var csv = PlayerPrefs.GetString(KeyListKey);
            return new List<string>(csv.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        }

        private static void RegisterKeyInEditor(string key)
        {
            var keys = GetAllKeysInEditor();
            if (keys.Contains(key))
            {
                return;
            }

            keys.Add(key);
            PlayerPrefs.SetString(KeyListKey, string.Join(",", keys));
        }

        public static void DeleteEditorOnly(string key)
        {
            PlayerPrefs.DeleteKey(key);

            var keys = GetAllKeysInEditor();
            if (keys.Remove(key))
            {
                PlayerPrefs.SetString(KeyListKey, string.Join(",", keys));
            }

            PlayerPrefs.Save();
        }
#endif
    }
}
