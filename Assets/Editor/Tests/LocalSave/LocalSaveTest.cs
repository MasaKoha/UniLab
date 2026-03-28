using NUnit.Framework;
using UnityEngine;
using UniLab.Persistence;

namespace UniLab.Tests.EditMode.Persistence
{
    public class LocalSaveTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [Test]
        public void SaveAndLoad_ReturnsEquivalentData()
        {
            var data = new TestData { Value = 123, Name = "TestUser" };
            LocalSave.Save(data);

            var loaded = LocalSave.Load<TestData>();

            Assert.AreEqual(data.Value, loaded.Value);
            Assert.AreEqual(data.Name, loaded.Name);
        }

        [Test]
        public void Delete_LoadReturnsDefaultValue()
        {
            var data = new TestData { Value = 456, Name = "ToDelete" };
            LocalSave.Save(data);
            LocalSave.Delete<TestData>();

            var loaded = LocalSave.Load<TestData>();

            Assert.AreNotEqual(data.Value, loaded.Value);
            Assert.AreNotEqual(data.Name, loaded.Name);
        }

        [Test]
        public void DeleteAll_LoadReturnsDefaultValue()
        {
            var data = new TestData { Value = 789, Name = "AllGone" };
            LocalSave.Save(data);
            LocalSave.DeleteAll();

            var loaded = LocalSave.Load<TestData>();

            Assert.AreNotEqual(data.Value, loaded.Value);
            Assert.AreNotEqual(data.Name, loaded.Name);
        }

        [Test]
        public void Load_WhenNeverSaved_ReturnsDefaultValue()
        {
            var loaded = LocalSave.Load<TestData>();

            Assert.AreEqual(0, loaded.Value);
            Assert.IsNull(loaded.Name);
        }

        // --- Editor key registry tests ---

        [Test]
        public void Save_RegistersKeyInEditorAndPlayerPrefsHasKey()
        {
            var data = new SampleData { Score = 10 };
            LocalSave.Save(data);

            var key = typeof(SampleData).FullName;
            var registeredKeys = LocalSave.GetAllKeysInEditor();

            Assert.IsTrue(registeredKeys.Contains(key), "Key should be present in GetAllKeysInEditor().");
            Assert.IsTrue(PlayerPrefs.HasKey(key), "PlayerPrefs should have the key after Save.");
        }

        [Test]
        public void DeleteEditorOnly_RemovesKeyFromRegistryAndPlayerPrefs()
        {
            var data = new SampleData { Score = 20 };
            LocalSave.Save(data);

            var key = typeof(SampleData).FullName;
            LocalSave.DeleteEditorOnly(key);

            var registeredKeys = LocalSave.GetAllKeysInEditor();

            Assert.IsFalse(registeredKeys.Contains(key), "Key should be removed from GetAllKeysInEditor() after DeleteEditorOnly.");
            Assert.IsFalse(PlayerPrefs.HasKey(key), "PlayerPrefs should not have the key after DeleteEditorOnly.");
        }

        [Test]
        public void DirectPlayerPrefsDeleteKey_LeavesKeyInRegistryButNotInPlayerPrefs()
        {
            // This scenario represents the "Not saved" state shown in LocalSaveEditorWindow Section 2.
            // The registry retains the key, but PlayerPrefs no longer has the actual value.
            var data = new SampleData { Score = 30 };
            LocalSave.Save(data);

            var key = typeof(SampleData).FullName;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            var registeredKeys = LocalSave.GetAllKeysInEditor();

            Assert.IsTrue(registeredKeys.Contains(key), "Key should still be present in registry after direct PlayerPrefs.DeleteKey.");
            Assert.IsFalse(PlayerPrefs.HasKey(key), "PlayerPrefs should not have the key after direct deletion.");
        }

        [Test]
        public void DeleteAll_ClearsRegistryAndAllPlayerPrefsKeys()
        {
            LocalSave.Save(new SampleData { Score = 1 });
            LocalSave.Save(new SampleData2 { Name = "Alice" });

            var key1 = typeof(SampleData).FullName;
            var key2 = typeof(SampleData2).FullName;

            LocalSave.DeleteAll();

            var registeredKeys = LocalSave.GetAllKeysInEditor();

            Assert.IsEmpty(registeredKeys, "Registry should be empty after DeleteAll.");
            Assert.IsFalse(PlayerPrefs.HasKey(key1), "PlayerPrefs should not have key1 after DeleteAll.");
            Assert.IsFalse(PlayerPrefs.HasKey(key2), "PlayerPrefs should not have key2 after DeleteAll.");
        }

        [System.Serializable]
        public class TestData
        {
            public int Value;
            public string Name;
        }

        [System.Serializable]
        private sealed class SampleData
        {
            public int Score;
        }

        [System.Serializable]
        private sealed class SampleData2
        {
            public string Name;
        }
    }
}
