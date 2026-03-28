using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using MessagePack;
using R3;
using UniLab.Common;
using UniLab.Common.Utility;
using UnityEngine;
using UnityEngine.Networking;

namespace UniLab.Feature.MasterData
{
    public abstract class MasterManager<T> : SingletonPureClass<T> where T : MasterManager<T>, new()
    {
        private const string MasterExtension = ".master";
        private const string CatalogExtension = ".catalog";
        private byte[] _key;
        private byte[] _iv;
        public string SavePath => $"{Application.persistentDataPath}/MasterData";
        private string CatalogFileName => Convert.ToBase64String(Encoding.UTF8.GetBytes(nameof(MasterCatalog))) + CatalogExtension;

        // 型ごとにマスターを保存
        private readonly Dictionary<Type, object> _masters = new();
        private readonly Subject<string> _onError = new();
        public Observable<string> OnError => _onError;

        protected abstract List<Type> MasterList { get; }

        private string _url;

        public void Initialize(string url)
        {
            _url = url;
        }

        public void SetKey(byte[] key, byte[] iv)
        {
            _key = key;
            _iv = iv;
#if UNITY_EDITOR
            // エディタで鍵をセットした場合はローカルにも保存しておく
            SaveAesKey(key, iv);
#endif
        }

        public async UniTask LoadMastersAsync()
        {
            var types = MasterList;
            await EnsureCatalogAndDownloadsAsync(_url, types.Select(t => t.Name));
            foreach (var type in types)
            {
                await LoadMasterFromDiskAsync(type);
            }
        }

        public TMaster GetMaster<TMaster>() where TMaster : MasterBase
        {
            _masters.TryGetValue(typeof(TMaster), out var master);
            return master as TMaster;
        }

        private MasterCatalog[] LoadCatalogFromDisk()
        {
            var loadPath = Path.Combine(SavePath, CatalogFileName);
            if (!File.Exists(loadPath))
            {
                return null;
            }

            try
            {
                var bytes = File.ReadAllBytes(loadPath);
                var container = MessagePackSerializer.Deserialize<CatalogContainer>(bytes);
                if (container?.Catalogs != null && container.Catalogs.Length > 0)
                {
                    return container.Catalogs;
                }

                return MessagePackSerializer.Deserialize<MasterCatalog[]>(bytes);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"Failed to read catalog file at {loadPath}: {e.Message}");
                return null;
            }
            catch (MessagePackSerializationException e)
            {
                Debug.LogWarning($"Failed to deserialize catalog at {loadPath}: {e.Message}");
                return null;
            }
        }

        private async UniTask LoadMasterFromDiskAsync(Type masterType)
        {
            if (masterType == null)
            {
                return;
            }

            var base64Name = Convert.ToBase64String(Encoding.UTF8.GetBytes(masterType.Name));
            var loadPath = Path.Combine(SavePath, $"{base64Name}{MasterExtension}");
            if (!File.Exists(loadPath))
            {
                return;
            }

            var encrypted = await File.ReadAllBytesAsync(loadPath);
            var decrypted = AesEncryptionUtility.Decrypt(encrypted, _key, _iv);
            var master = MessagePackSerializer.Deserialize(masterType, decrypted) as MasterBase;
            if (master != null)
            {
                _masters[masterType] = master;
            }
        }

        private string GetLocalMasterPath(string masterName)
        {
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(masterName));
            return Path.Combine(SavePath, $"{base64}{MasterExtension}");
        }

        // 暗号化済みマスターのバイナリをそのままハッシュ化
        private static string ComputeFileHash(string filePath)
        {
            using var sha = SHA256.Create();
            var bytes = File.ReadAllBytes(filePath);
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }

        private IReadOnlyCollection<string> GetDownloadTargets(IEnumerable<MasterCatalog> catalogEntries)
        {
            if (catalogEntries == null)
            {
                return Array.Empty<string>();
            }

            var downloadTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in catalogEntries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.MasterName))
                {
                    continue;
                }

                if (!NeedsDownload(entry))
                {
                    continue;
                }

                downloadTargets.Add(entry.MasterName);
            }

            return downloadTargets;
        }

        private bool NeedsDownload(MasterCatalog entry)
        {
            if (string.IsNullOrEmpty(entry.Hash))
            {
                return true;
            }

            var localPath = GetLocalMasterPath(entry.MasterName);
            if (!File.Exists(localPath))
            {
                return true;
            }

            return !string.Equals(ComputeFileHash(localPath), entry.Hash, StringComparison.Ordinal);
        }

        private async UniTask<string> DownloadCatalogAsync(string baseUrl)
        {
            return await DownloadFileAsync(baseUrl, CatalogFileName);
        }

        private async UniTask<string> DownloadMasterAsync(string baseUrl, string masterId)
        {
            var fileName = $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(masterId))}{MasterExtension}";
            return await DownloadFileAsync(baseUrl, fileName);
        }

        private async UniTask<string> DownloadFileAsync(string baseUrl, string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new ArgumentException("Base URL is required.", nameof(baseUrl));
                }

                var requestUrl = BuildRequestUrl(baseUrl, fileName);
                using var request = UnityWebRequest.Get(requestUrl);
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var body = request.downloadHandler?.text;
                    var reason = string.IsNullOrWhiteSpace(body) ? request.error : body;
                    throw new InvalidOperationException($"Download failed: {requestUrl} => {request.responseCode} {reason}");
                }

                var savePath = Path.Combine(SavePath, fileName);
                EnsureSaveDirectoryExists();
                await File.WriteAllBytesAsync(savePath, request.downloadHandler.data);
                Debug.Log($"Downloaded {requestUrl} to {savePath}");
                return savePath;
            }
            catch (Exception e)
            {
                _onError.OnNext(e.Message);
                throw;
            }
        }

        private static string BuildRequestUrl(string baseUrl, string fileName)
        {
            return baseUrl.EndsWith("/", StringComparison.Ordinal)
                ? baseUrl + fileName
                : $"{baseUrl}/{fileName}";
        }

        private void EnsureSaveDirectoryExists()
        {
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }
        }

        private async UniTask EnsureCatalogAndDownloadsAsync(string baseUrl, IEnumerable<string> requiredMasterIds)
        {
            await DownloadCatalogAsync(baseUrl);
            var catalogEntries = LoadCatalogFromDisk();
            if (catalogEntries == null || catalogEntries.Length == 0)
            {
                var message = $"Catalog load failed or empty. File : {CatalogFileName}";
                _onError.OnNext(message);
                throw new InvalidOperationException(message);
            }

            var downloadTargets = GetDownloadTargets(catalogEntries).ToList();
            if (requiredMasterIds != null)
            {
                var requiredSet = new HashSet<string>(requiredMasterIds, StringComparer.Ordinal);
                downloadTargets = downloadTargets.Where(requiredSet.Contains).ToList();
            }

            foreach (var masterName in downloadTargets)
            {
                await DownloadMasterAsync(baseUrl, masterName);
            }
        }

#if UNITY_EDITOR
        /// エディタ限定: AESキーを保存/読み込み
        /// クライアントでマスターの中身を見るために保存
        public void SaveAesKey(byte[] key, byte[] iv)
        {
            var savePath = Path.Combine(SavePath, "aes_key.txt");
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }

            var keyString = Convert.ToBase64String(key);
            var ivString = Convert.ToBase64String(iv);

            File.WriteAllText(savePath, $"{keyString}\n{ivString}");
        }

        public void LoadAesKey(out byte[] key, out byte[] iv)
        {
            var savePath = Path.Combine(SavePath, "aes_key.txt");
            if (!File.Exists(savePath))
            {
                key = null;
                iv = null;
                return;
            }

            var lines = File.ReadAllLines(savePath);
            if (lines.Length < 2)
            {
                key = null;
                iv = null;
                return;
            }

            key = Convert.FromBase64String(lines[0]);
            iv = Convert.FromBase64String(lines[1]);
        }

        public void SaveMaster<TMaster>(TMaster master) where TMaster : MasterBase
        {
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }

            var base64Name = Convert.ToBase64String(Encoding.UTF8.GetBytes(master.MasterId));
            var savePath = Path.Combine(SavePath, $"{base64Name}{MasterExtension}");
            var bytes = MessagePackSerializer.Serialize(master);
            var encrypted = AesEncryptionUtility.Encrypt(bytes, _key, _iv);
            File.WriteAllBytes(savePath, encrypted);
            _masters[typeof(TMaster)] = master;
        }
#endif
    }
}