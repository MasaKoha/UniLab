using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UniLab.Common.Utility;

namespace UniLab.Storage
{
    /// <summary>
    /// ILocalStorage implementation that persists data as AES-encrypted JSON files
    /// under Application.persistentDataPath. Supports optional TTL-based expiry.
    /// </summary>
    public class EncryptedLocalStorage : ILocalStorage
    {
        private readonly StorageKeyManager _keyManager;

        /// <summary>
        /// Initializes a new instance with a lazily-loaded AES key/IV pair.
        /// </summary>
        public EncryptedLocalStorage()
        {
            _keyManager = new StorageKeyManager();
        }

        /// <inheritdoc/>
        public void Save<T>(string key, T data, TimeSpan? ttl = null)
        {
            var expiresAt = ttl.HasValue
                ? DateTimeOffset.UtcNow.Add(ttl.Value).ToUnixTimeSeconds()
                : 0L;

            var entry = new StorageEntry<T> { Data = data, ExpiresAt = expiresAt };
            var json = JsonUtility.ToJson(entry);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encryptedBytes = AesEncryptionUtility.Encrypt(plainBytes, _keyManager.Key, _keyManager.Iv);
            var base64 = Convert.ToBase64String(encryptedBytes);
            File.WriteAllText(GetFilePath(key), base64, Encoding.UTF8);
        }

        /// <inheritdoc/>
        public T Load<T>(string key) where T : new()
        {
            var filePath = GetFilePath(key);
            if (!File.Exists(filePath))
            {
                return new T();
            }

            var base64 = File.ReadAllText(filePath, Encoding.UTF8);
            var encryptedBytes = Convert.FromBase64String(base64);
            var plainBytes = AesEncryptionUtility.Decrypt(encryptedBytes, _keyManager.Key, _keyManager.Iv);
            var json = Encoding.UTF8.GetString(plainBytes);
            var entry = JsonUtility.FromJson<StorageEntry<T>>(json);

            // Treat ExpiresAt == 0 as no expiry; positive values are Unix timestamps.
            if (entry.ExpiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() > entry.ExpiresAt)
            {
                Delete(key);
                return new T();
            }

            return entry.Data;
        }

        /// <inheritdoc/>
        public void Delete(string key)
        {
            var filePath = GetFilePath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        /// <inheritdoc/>
        public bool Exists(string key)
        {
            var filePath = GetFilePath(key);
            if (!File.Exists(filePath))
            {
                return false;
            }

            // Reuse Load's expiry logic: if the entry is expired it will be deleted and a
            // default instance is returned, but we cannot distinguish that from "not found"
            // without re-reading. Reading the entry is the most reliable approach here.
            var base64 = File.ReadAllText(filePath, Encoding.UTF8);
            var encryptedBytes = Convert.FromBase64String(base64);
            var plainBytes = AesEncryptionUtility.Decrypt(encryptedBytes, _keyManager.Key, _keyManager.Iv);
            var json = Encoding.UTF8.GetString(plainBytes);

            // StorageEntry<object> cannot be used with JsonUtility due to generic constraints;
            // use a lightweight expiry-only struct instead.
            var expiryProbe = JsonUtility.FromJson<StorageExpiryProbe>(json);
            if (expiryProbe.ExpiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiryProbe.ExpiresAt)
            {
                Delete(key);
                return false;
            }

            return true;
        }

        private static string GetFilePath(string key)
        {
            return Path.Combine(Application.persistentDataPath, $"{key}.dat");
        }

        // --- Nested types ---

        /// <summary>
        /// Wraps the actual payload with expiry metadata so TTL can be enforced on load.
        /// </summary>
        [Serializable]
        private class StorageEntry<T>
        {
            public T Data;

            /// <summary>Unix timestamp (seconds). 0 means no expiry.</summary>
            public long ExpiresAt;
        }

        /// <summary>
        /// Used in Exists() to check expiry without knowing the generic type T.
        /// JsonUtility only populates fields that exist in the JSON, so unknown fields are ignored.
        /// </summary>
        [Serializable]
        private class StorageExpiryProbe
        {
            public long ExpiresAt;
        }

        /// <summary>
        /// Manages the AES key and IV used for encryption. Generates them once per device
        /// and persists them in PlayerPrefs so data survives app restarts.
        /// </summary>
        private class StorageKeyManager
        {
            private const string AesKeyPrefsKey = "UniLab.Storage.AesKey";
            private const string AesIvPrefsKey = "UniLab.Storage.AesIv";
            private const int AesKeySize = 32; // 256-bit
            private const int AesIvSize = 16;  // 128-bit

            /// <summary>AES encryption key (256-bit).</summary>
            public byte[] Key { get; }

            /// <summary>AES initialization vector (128-bit).</summary>
            public byte[] Iv { get; }

            public StorageKeyManager()
            {
                Key = LoadOrGenerateBytes(AesKeyPrefsKey, AesKeySize);
                Iv = LoadOrGenerateBytes(AesIvPrefsKey, AesIvSize);
            }

            private static byte[] LoadOrGenerateBytes(string prefsKey, int byteCount)
            {
                if (PlayerPrefs.HasKey(prefsKey))
                {
                    return Convert.FromBase64String(PlayerPrefs.GetString(prefsKey));
                }

                var bytes = new byte[byteCount];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(bytes);
                PlayerPrefs.SetString(prefsKey, Convert.ToBase64String(bytes));
                PlayerPrefs.Save();
                return bytes;
            }
        }
    }
}
