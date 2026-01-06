using NUnit.Framework;
using UnityEngine;

namespace Qvou.UnityCore.LocalSave.Editor.Test
{
    public class LocalSaveTests
    {
#if false // ファイルの書き込み読み込みが発生するので、このテストはコメントアウト。書き換えるときに true に変更してアクセスすること
        [SetUp]
        public void Setup()
        {
            // 毎テスト前にクリア
            PlayerPrefs.DeleteAll();
        }

        [Test]
        public void SaveAndLoadTest()
        {
            var data = new TestData { Value = 123, Name = "TestUser" };
            LocalSave.Save(data);

            var loaded = LocalSave.Load<TestData>();

            Assert.AreEqual(data.Value, loaded.Value);
            Assert.AreEqual(data.Name, loaded.Name);
        }

        [Test]
        public void DeleteTest()
        {
            var data = new TestData { Value = 456, Name = "ToDelete" };
            LocalSave.Save(data);
            LocalSave.Delete<TestData>();
            var loaded = LocalSave.Load<TestData>();
            Assert.AreNotEqual(data.Value, loaded.Value);
            Assert.AreNotEqual(data.Name, loaded.Name);
        }

        [Test]
        public void DeleteAllTest()
        {
            var data = new TestData { Value = 789, Name = "AllGone" };
            LocalSave.Save(data);
            LocalSave.DeleteAll();
            var loaded = LocalSave.Load<TestData>();
            Assert.AreNotEqual(data.Value, loaded.Value);
            Assert.AreNotEqual(data.Name, loaded.Name);
        }

        [Test]
        public void LoadDefaultValueWhenNotSaved()
        {
            var loaded = LocalSave.Load<TestData>();
            Assert.AreEqual(0, loaded.Value);
            Assert.IsNull(loaded.Name);
        }
    }

    [System.Serializable]
    public class TestData
    {
        public int Value;
        public string Name;

        public override bool Equals(object obj)
        {
            if (obj is TestData other)
                return Value == other.Value && Name == other.Name;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ Name.GetHashCode();
        }
#endif
    }
}