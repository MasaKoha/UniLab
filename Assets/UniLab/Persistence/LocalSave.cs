using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniLab.Persistence
{
    public static class LocalSave
    {
        // 既定は後方互換のため JSON。アプリ起動時に SetSerializer で付け替える。
        private static ILocalSaveSerializer _serializer = new JsonLocalSaveSerializer();

        /// <summary>
        /// シリアライズ方式を差し替える。Save / Load より前（アプリ起動時の合成ルート）で
        /// 一度だけ呼ぶ想定。方式を変えると既存の保存データは読めなくなるため、
        /// 移行が必要な場合は別途対応すること。
        /// </summary>
        public static void SetSerializer(ILocalSaveSerializer serializer)
        {
            _serializer = serializer;
        }

        private static string GetKeyName<TData>() => typeof(TData).FullName;

        public static void Save<TData>(TData data)
        {
            var bytes = _serializer.Serialize(data);
            var base64 = Convert.ToBase64String(bytes);
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
            var bytes = Convert.FromBase64String(base64);
            return _serializer.Deserialize<TData>(bytes);
        }

        public static void Delete<T>()
        {
            var key = GetKeyName<T>();
            PlayerPrefs.DeleteKey(key);
#if UNITY_EDITOR
            RegisterKeyInEditor(key);
#endif
        }

        /// <summary>
        /// Deletes all LocalSave data.
        /// In the Editor, only keys registered by LocalSave are removed so that
        /// PlayerPrefs entries from other systems are preserved.
        /// In runtime builds, PlayerPrefs.DeleteAll() is used as no registry is available.
        /// </summary>
        public static void DeleteAll()
        {
#if UNITY_EDITOR
            foreach (var key in GetAllKeysInEditor())
            {
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.DeleteKey(KeyListKey);
            PlayerPrefs.Save();
#else
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
#endif
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
