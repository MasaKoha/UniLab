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

        private static string GetBackupKeyName<TData>() => GetKeyName<TData>() + ".bak";

        private static string GetCorruptKeyName<TData>() => GetKeyName<TData>() + ".corrupt";

        /// <summary>
        /// データを保存する。直列化に成功した場合のみ既存データへ書き込むことで、
        /// 直列化失敗による破損窓を作らない。書き込み前に旧世代をバックアップキーへ退避する。
        /// </summary>
        public static void Save<TData>(TData data)
        {
            var bytes = _serializer.Serialize(data);
            var base64 = Convert.ToBase64String(bytes);
            var key = GetKeyName<TData>();
            var backupKey = GetBackupKeyName<TData>();

            if (PlayerPrefs.HasKey(key))
            {
                var previousBase64 = PlayerPrefs.GetString(key);
                PlayerPrefs.SetString(backupKey, previousBase64);
            }

            PlayerPrefs.SetString(key, base64);
            PlayerPrefs.Save();
#if UNITY_EDITOR
            RegisterKeyInEditor(key);
            RegisterKeyInEditor(backupKey);
#endif
        }

        /// <summary>
        /// データを読み込む。メインデータが破損している場合はバックアップ世代へフォールバックし、
        /// それも失敗した場合は既定値を返して起動不能（無言ハング）を防ぐ。
        /// </summary>
        public static TData Load<TData>() where TData : new()
        {
            var key = GetKeyName<TData>();
            if (!PlayerPrefs.HasKey(key))
            {
                return new TData();
            }

            var mainBase64 = PlayerPrefs.GetString(key);
            if (TryDeserialize<TData>(mainBase64, out var mainResult))
            {
                return mainResult;
            }

            Debug.LogWarning($"[LocalSave] Failed to deserialize '{key}'. Falling back to backup.");
            PlayerPrefs.SetString(GetCorruptKeyName<TData>(), mainBase64);

            var backupKey = GetBackupKeyName<TData>();
            if (PlayerPrefs.HasKey(backupKey))
            {
                var backupBase64 = PlayerPrefs.GetString(backupKey);
                if (TryDeserialize<TData>(backupBase64, out var backupResult))
                {
                    PlayerPrefs.SetString(key, backupBase64);
                    PlayerPrefs.Save();
                    return backupResult;
                }
            }

            Debug.LogWarning($"[LocalSave] Backup for '{key}' is also unavailable or corrupted. Returning default instance.");
            return new TData();
        }

        /// <summary>
        /// Base64 文字列のデコードと直列化解除を try/catch に集約するヘルパー。
        /// 呼び出し側のネストを浅く保つ。
        /// </summary>
        private static bool TryDeserialize<TData>(string base64, out TData result)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                result = _serializer.Deserialize<TData>(bytes);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[LocalSave] Deserialize failed: {exception.Message}");
                result = default;
                return false;
            }
        }

        public static void Delete<T>()
        {
            var key = GetKeyName<T>();
            var backupKey = GetBackupKeyName<T>();
            var corruptKey = GetCorruptKeyName<T>();
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(backupKey);
            PlayerPrefs.DeleteKey(corruptKey);
#if UNITY_EDITOR
            RegisterKeyInEditor(key);
            RegisterKeyInEditor(backupKey);
            RegisterKeyInEditor(corruptKey);
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
