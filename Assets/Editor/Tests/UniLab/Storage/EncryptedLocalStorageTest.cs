using System;
using System.IO;
using NUnit.Framework;
using UniLab.Storage;
using UnityEngine;

namespace UniLab.Tests.EditMode.Storage
{
    public class EncryptedLocalStorageTest
    {
        private const string TestKey = "unilab_test_encrypted";
        private ILocalStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _storage = new EncryptedLocalStorage();
            _storage.Delete(TestKey);
        }

        [TearDown]
        public void TearDown()
        {
            _storage.Delete(TestKey);
        }

        [Test]
        public void Save_ThenLoad_ReturnsOriginalValue()
        {
            var data = new TestPayload { Value = 42, Name = "hello" };
            _storage.Save(TestKey, data);

            var loaded = _storage.Load<TestPayload>(TestKey);

            Assert.AreEqual(42, loaded.Value);
            Assert.AreEqual("hello", loaded.Name);
        }

        [Test]
        public void Load_NonExistentKey_ReturnsNewInstance()
        {
            var loaded = _storage.Load<TestPayload>("does_not_exist");

            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.Value);
            Assert.IsNull(loaded.Name);
        }

        [Test]
        public void Delete_RemovesEntry()
        {
            _storage.Save(TestKey, new TestPayload { Value = 1 });
            _storage.Delete(TestKey);

            var loaded = _storage.Load<TestPayload>(TestKey);

            Assert.AreEqual(0, loaded.Value);
        }

        [Test]
        public void Exists_ReturnsTrueAfterSave()
        {
            _storage.Save(TestKey, new TestPayload { Value = 1 });

            Assert.IsTrue(_storage.Exists(TestKey));
        }

        [Test]
        public void Exists_ReturnsFalseBeforeSave()
        {
            Assert.IsFalse(_storage.Exists(TestKey));
        }

        [Test]
        public void Exists_ReturnsFalseAfterDelete()
        {
            _storage.Save(TestKey, new TestPayload { Value = 1 });
            _storage.Delete(TestKey);

            Assert.IsFalse(_storage.Exists(TestKey));
        }

        [Test]
        public void Save_WithExpiredTtl_LoadReturnsNewInstance()
        {
            // TTL of -1 second is already expired at the moment of saving.
            _storage.Save(TestKey, new TestPayload { Value = 99 }, TimeSpan.FromSeconds(-1));

            var loaded = _storage.Load<TestPayload>(TestKey);

            Assert.AreEqual(0, loaded.Value);
        }

        [Test]
        public void Save_WithFutureTtl_LoadReturnsValue()
        {
            _storage.Save(TestKey, new TestPayload { Value = 77 }, TimeSpan.FromHours(1));

            var loaded = _storage.Load<TestPayload>(TestKey);

            Assert.AreEqual(77, loaded.Value);
        }

        [Test]
        public void Exists_ReturnsFalseForExpiredEntry()
        {
            _storage.Save(TestKey, new TestPayload { Value = 1 }, TimeSpan.FromSeconds(-1));

            Assert.IsFalse(_storage.Exists(TestKey));
        }

        [Test]
        public void DifferentKeys_DoNotCollide()
        {
            const string keyA = "unilab_test_a";
            const string keyB = "unilab_test_b";

            _storage.Save(keyA, new TestPayload { Value = 1 });
            _storage.Save(keyB, new TestPayload { Value = 2 });

            Assert.AreEqual(1, _storage.Load<TestPayload>(keyA).Value);
            Assert.AreEqual(2, _storage.Load<TestPayload>(keyB).Value);

            _storage.Delete(keyA);
            _storage.Delete(keyB);
        }

        [Serializable]
        private class TestPayload
        {
            public int Value;
            public string Name;
        }
    }
}
