using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using MessagePack;
using NUnit.Framework;
using UniLab.Common.Utility;
using UniLab.MasterData;
using UnityEngine.TestTools;

namespace UniLab.Tests.EditMode.MasterData
{
    // Concrete master used only by these tests. Mirrors the shape of a real shipped master.
    [MessagePackObject]
    public class LocalLoaderItemMaster : MasterBase
    {
        [MessagePackObject]
        public class Record
        {
            [Key("id")] public int Id;
            [Key("name")] public string Name;
        }

        [Key("records")] public Record[] Records { get; set; }

        public LocalLoaderItemMaster()
        {
            MasterId = GetType().Name;
            Hash = string.Empty;
        }
    }

    // Test subclass that redirects the local source directory to a temp folder so the File IO path
    // (the #else branch of ReadAllBytesFromLocalSourceAsync) can be exercised in the editor.
    internal sealed class LocalLoaderTestMasterManager : MasterManager<LocalLoaderTestMasterManager>
    {
        public string SourceDirectory;
        protected override List<Type> MasterList => new() { typeof(LocalLoaderItemMaster) };
        protected override string LocalMasterSourceDirectory => SourceDirectory;
    }

    public class MasterDataLocalLoaderTest
    {
        // Same key/iv shape AesEncryptionUtility expects (16 bytes each).
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("1234567890123456");
        private static readonly byte[] Iv = Encoding.UTF8.GetBytes("abcdef9876543210");

        private LocalLoaderTestMasterManager _manager;
        private string _sourceDirectory;

        [SetUp]
        public void SetUp()
        {
            _sourceDirectory = Path.Combine(Path.GetTempPath(), "UniLabMasterLocalLoaderTest", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_sourceDirectory);

            _manager = LocalLoaderTestMasterManager.Instance;
            _manager.SetKey(Key, Iv);
            _manager.SourceDirectory = _sourceDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
            if (Directory.Exists(_sourceDirectory))
            {
                Directory.Delete(_sourceDirectory, true);
            }
        }

        // --- Helpers ---

        // Mirrors MasterManager's naming convention: Base64(masterName) + ".master".
        private static string GetExpectedFileName(string masterName)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(masterName)) + ".master";
        }

        private void WriteEncryptedMaster(LocalLoaderItemMaster master)
        {
            var bytes = MessagePackSerializer.Serialize(master);
            var encrypted = AesEncryptionUtility.Encrypt(bytes, Key, Iv);
            var path = Path.Combine(_sourceDirectory, GetExpectedFileName(master.MasterId));
            File.WriteAllBytes(path, encrypted);
        }

        private static LocalLoaderItemMaster CreateSampleMaster()
        {
            return new LocalLoaderItemMaster
            {
                Records = new[]
                {
                    new LocalLoaderItemMaster.Record { Id = 1, Name = "Sword" },
                    new LocalLoaderItemMaster.Record { Id = 2, Name = "Shield" },
                },
            };
        }

        // --- Tests ---

        // UniTask backed by File.ReadAllBytesAsync does not complete synchronously, so these run as
        // coroutine-driven UnityTests (ToCoroutine) rather than blocking on GetAwaiter().GetResult().

        [UnityTest]
        public IEnumerator LoadMastersFromStreamingAssets_DecryptsAndDeserializes() => UniTask.ToCoroutine(async () =>
        {
            var master = CreateSampleMaster();
            WriteEncryptedMaster(master);

            await _manager.LoadMastersFromStreamingAssetsAsync();

            var loaded = _manager.GetMaster<LocalLoaderItemMaster>();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(nameof(LocalLoaderItemMaster), loaded.MasterId);
            Assert.AreEqual(2, loaded.Records.Length);
            Assert.AreEqual("Sword", loaded.Records[0].Name);
            Assert.AreEqual(2, loaded.Records[1].Id);
        });

        [UnityTest]
        public IEnumerator LoadMastersFromStreamingAssets_UsesBase64FileNamingConvention() => UniTask.ToCoroutine(async () =>
        {
            var master = CreateSampleMaster();
            WriteEncryptedMaster(master);

            var expectedPath = Path.Combine(_sourceDirectory, GetExpectedFileName(nameof(LocalLoaderItemMaster)));
            Assert.IsTrue(File.Exists(expectedPath), "Encrypted master must be written with the Base64(name)+.master convention.");

            await _manager.LoadMastersFromStreamingAssetsAsync();
            Assert.IsNotNull(_manager.GetMaster<LocalLoaderItemMaster>());
        });

        [UnityTest]
        public IEnumerator LoadMastersFromStreamingAssets_MissingFile_LeavesMasterNull() => UniTask.ToCoroutine(async () =>
        {
            // No file written for the master.
            await _manager.LoadMastersFromStreamingAssetsAsync();
            Assert.IsNull(_manager.GetMaster<LocalLoaderItemMaster>());
        });

        [UnityTest]
        public IEnumerator LoadMastersFromStreamingAssets_WrongKey_Throws() => UniTask.ToCoroutine(async () =>
        {
            WriteEncryptedMaster(CreateSampleMaster());

            // Re-key with a different key so decryption of the existing payload fails.
            var wrongKey = Encoding.UTF8.GetBytes("6543210987654321");
            _manager.SetKey(wrongKey, Iv);

            // Wrong key fails AES padding (CryptographicException); in the rare case padding is
            // coincidentally valid, MessagePack deserialization of the garbage rejects it instead.
            var threw = false;
            try
            {
                await _manager.LoadMastersFromStreamingAssetsAsync();
            }
            catch (Exception)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Loading with the wrong key must throw.");
        });
    }
}
