using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UniLab.Common.Utility;

namespace UniLab.Persistence
{
    /// <summary>
    /// ILocalStorage implementation that persists data as AES-encrypted blobs under
    /// Application.persistentDataPath. Supports optional TTL-based expiry.
    /// シリアライズ方式は ILocalSaveSerializer で差し替え可能（既定は JSON）。
    /// 保存形式は「8バイトの有効期限ヘッダ + 直列化済みペイロード」を AES 暗号化したもの。
    /// 有効期限をペイロードと分離することで、シリアライザ非依存に期限判定できる。
    /// </summary>
    public class EncryptedLocalStorage : ILocalStorage
    {
        // 有効期限ヘッダのバイト数（long = Unixエポック秒）。
        private const int ExpiryHeaderSize = sizeof(long);

        private readonly StorageKeyManager _keyManager;
        private readonly ILocalSaveSerializer _serializer;

        /// <summary>
        /// Initializes a new instance with a lazily-loaded AES key/IV pair.
        /// serializer 省略時は JSON（JsonLocalSaveSerializer）を使う。
        /// </summary>
        public EncryptedLocalStorage(ILocalSaveSerializer serializer = null)
        {
            _keyManager = new StorageKeyManager();
            _serializer = serializer ?? new JsonLocalSaveSerializer();
        }

        /// <inheritdoc/>
        public void Save<T>(string key, T data, TimeSpan? ttl = null)
        {
            var expiresAt = ttl.HasValue
                ? DateTimeOffset.UtcNow.Add(ttl.Value).ToUnixTimeSeconds()
                : 0L;

            var payload = _serializer.Serialize(data);
            var plainBytes = new byte[ExpiryHeaderSize + payload.Length];
            // 先頭8バイトに有効期限、その後ろにペイロードを連結する。
            BitConverter.GetBytes(expiresAt).CopyTo(plainBytes, 0);
            payload.CopyTo(plainBytes, ExpiryHeaderSize);

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

            var plainBytes = ReadDecrypted(filePath);
            var expiresAt = BitConverter.ToInt64(plainBytes, 0);
            if (IsExpired(expiresAt))
            {
                Delete(key);
                return new T();
            }

            var payload = new byte[plainBytes.Length - ExpiryHeaderSize];
            Array.Copy(plainBytes, ExpiryHeaderSize, payload, 0, payload.Length);
            return _serializer.Deserialize<T>(payload);
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

            // 期限ヘッダだけ見れば判定できるため、ペイロード（型 T）の復元は不要。
            var plainBytes = ReadDecrypted(filePath);
            var expiresAt = BitConverter.ToInt64(plainBytes, 0);
            if (IsExpired(expiresAt))
            {
                Delete(key);
                return false;
            }

            return true;
        }

        // ファイルを読み、Base64復号 + AES復号した平文（期限ヘッダ + ペイロード）を返す。
        private byte[] ReadDecrypted(string filePath)
        {
            var base64 = File.ReadAllText(filePath, Encoding.UTF8);
            var encryptedBytes = Convert.FromBase64String(base64);
            return AesEncryptionUtility.Decrypt(encryptedBytes, _keyManager.Key, _keyManager.Iv);
        }

        // ExpiresAt == 0 は無期限。正値は Unixエポック秒で、現在時刻を過ぎていれば期限切れ。
        private static bool IsExpired(long expiresAt)
        {
            return expiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt;
        }

        private static string GetFilePath(string key)
        {
            return Path.Combine(Application.persistentDataPath, $"{key}.dat");
        }

        // --- Nested types ---

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
