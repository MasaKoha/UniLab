using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UniLab.MasterData;

namespace UniLab.Tests.EditMode.MasterData
{
    // Minimal concrete subclass — exposes SavePath so tests can place files correctly.
    internal sealed class TestMasterManager : MasterManager<TestMasterManager>
    {
        protected override List<Type> MasterList => new();
    }

    public class MasterManagerDownloadTargetsTest
    {
        private TestMasterManager _manager;
        private readonly List<string> _createdFiles = new();

        [SetUp]
        public void SetUp()
        {
            _manager = TestMasterManager.Instance;
            if (!Directory.Exists(_manager.SavePath))
            {
                Directory.CreateDirectory(_manager.SavePath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var file in _createdFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            _createdFiles.Clear();
            _manager.Dispose();
        }

        // --- Helpers ---

        private IReadOnlyCollection<string> InvokeGetDownloadTargets(IEnumerable<MasterCatalog> entries)
        {
            var method = typeof(MasterManager<TestMasterManager>)
                .GetMethod("GetDownloadTargets", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "GetDownloadTargets not found via reflection.");
            return (IReadOnlyCollection<string>)method.Invoke(_manager, new object[] { entries });
        }

        // Mirrors MasterManager.GetLocalMasterPath naming convention.
        private string GetExpectedFilePath(string masterName)
        {
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(masterName));
            return Path.Combine(_manager.SavePath, base64 + ".master");
        }

        private string CreateMasterFile(string masterName, byte[] contents)
        {
            var path = GetExpectedFilePath(masterName);
            File.WriteAllBytes(path, contents);
            _createdFiles.Add(path);
            return path;
        }

        private static string ComputeHash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }

        // --- Tests ---

        [Test]
        public void GetDownloadTargets_NullCatalog_ReturnsEmpty()
        {
            var result = InvokeGetDownloadTargets(null);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GetDownloadTargets_EmptyCatalog_ReturnsEmpty()
        {
            var result = InvokeGetDownloadTargets(Array.Empty<MasterCatalog>());
            Assert.IsEmpty(result);
        }

        [Test]
        public void GetDownloadTargets_EntryWithNullMasterName_IsSkipped()
        {
            var entries = new[] { new MasterCatalog { MasterName = null, Hash = "abc" } };
            var result = InvokeGetDownloadTargets(entries);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GetDownloadTargets_EntryWithEmptyMasterName_IsSkipped()
        {
            var entries = new[] { new MasterCatalog { MasterName = "", Hash = "abc" } };
            var result = InvokeGetDownloadTargets(entries);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GetDownloadTargets_FileDoesNotExist_IsInDownloadList()
        {
            var entries = new[] { new MasterCatalog { MasterName = "MissingMaster", Hash = "some_hash" } };
            var result = InvokeGetDownloadTargets(entries);
            CollectionAssert.Contains(result, "MissingMaster");
        }

        [Test]
        public void GetDownloadTargets_FileExistsWithMatchingHash_NotInDownloadList()
        {
            const string masterName = "FreshMaster";
            var bytes = new byte[] { 10, 20, 30, 40 };
            CreateMasterFile(masterName, bytes);
            var expectedHash = ComputeHash(bytes);

            var entries = new[] { new MasterCatalog { MasterName = masterName, Hash = expectedHash } };
            var result = InvokeGetDownloadTargets(entries);

            CollectionAssert.DoesNotContain(result, masterName);
        }

        [Test]
        public void GetDownloadTargets_FileExistsWithDifferentHash_IsInDownloadList()
        {
            const string masterName = "StaleMaster";
            var bytes = new byte[] { 10, 20, 30, 40 };
            CreateMasterFile(masterName, bytes);

            var entries = new[] { new MasterCatalog { MasterName = masterName, Hash = "wrong_hash" } };
            var result = InvokeGetDownloadTargets(entries);

            CollectionAssert.Contains(result, masterName);
        }

        [Test]
        public void GetDownloadTargets_FileExistsWithEmptyEntryHash_IsInDownloadList()
        {
            // Empty remote hash means catalog is incomplete → treat as needing re-download.
            const string masterName = "NoHashMaster";
            var bytes = new byte[] { 1, 2, 3 };
            CreateMasterFile(masterName, bytes);

            var entries = new[] { new MasterCatalog { MasterName = masterName, Hash = "" } };
            var result = InvokeGetDownloadTargets(entries);

            CollectionAssert.Contains(result, masterName);
        }

        [Test]
        public void GetDownloadTargets_MixedEntries_OnlyStaleOnesReturned()
        {
            var freshBytes = new byte[] { 1, 2, 3 };
            var freshHash = ComputeHash(freshBytes);
            CreateMasterFile("FreshMaster", freshBytes);
            // "StaleMaster" intentionally not created on disk → needs download

            var entries = new[]
            {
                new MasterCatalog { MasterName = "FreshMaster", Hash = freshHash },
                new MasterCatalog { MasterName = "StaleMaster", Hash = "outdated" },
            };
            var result = InvokeGetDownloadTargets(entries);

            CollectionAssert.DoesNotContain(result, "FreshMaster");
            CollectionAssert.Contains(result, "StaleMaster");
        }

        [Test]
        public void GetDownloadTargets_NullEntry_DoesNotThrow()
        {
            var entries = new MasterCatalog[] { null };
            Assert.DoesNotThrow(() => InvokeGetDownloadTargets(entries));
        }
    }
}
