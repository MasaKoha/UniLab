using System.IO;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using NUnit.Framework;

namespace Qvou.UnityCore.MessagePack.Editor.Test
{
    public class MessagePackNestTests
    {
        [MessagePackObject]
        public class Outer
        {
            [Key(0)] public string Name { get; set; }
            [Key(1)] public Inner InnerObj { get; set; }
        }

        [MessagePackObject]
        public class Inner
        {
            [Key(0)] public int Value { get; set; }
        }

        [MessagePackObject]
        public class BaseOuter
        {
            [Key(0)] public string Name { get; set; }
        }

        [MessagePackObject]
        public class DerivedOuter : BaseOuter
        {
            [Key(1)] public int Age { get; set; }
        }

        [MessagePackObject]
        public class OuterArray
        {
            [Key(0)] public string Name { get; set; }
            [Key(1)] public Inner[] Items { get; set; }
        }

        [Test]
        public void SerializeAndDeserialize_ArrayOfMessagePackObject_ShouldWork()
        {
            var outer = new OuterArray
            {
                Name = "ArrayTest",
                Items = new[]
                {
                    new Inner { Value = 1 },
                    new Inner { Value = 2 },
                    new Inner { Value = 3 }
                }
            };

            var bytes = MessagePackSerializer.Serialize(outer);
            var restored = MessagePackSerializer.Deserialize<OuterArray>(bytes);

            Assert.AreEqual("ArrayTest", restored.Name);
            Assert.IsNotNull(restored.Items);
            Assert.AreEqual(3, restored.Items.Length);
            Assert.AreEqual(1, restored.Items[0].Value);
            Assert.AreEqual(2, restored.Items[1].Value);
            Assert.AreEqual(3, restored.Items[2].Value);
        }

        [Test]
        public void SerializeAndDeserialize_NestedMessagePackObject_ShouldWork()
        {
            var outer = new Outer
            {
                Name = "Test",
                InnerObj = new Inner { Value = 42 }
            };

            // シリアライズ
            var bytes = MessagePackSerializer.Serialize(outer);

            // デシリアライズ
            var restored = MessagePackSerializer.Deserialize<Outer>(bytes);

            Assert.AreEqual("Test", restored.Name);
            Assert.IsNotNull(restored.InnerObj);
            Assert.AreEqual(42, restored.InnerObj.Value);
        }

        [Test]
        public void SerializeToFile_And_DeserializeFromFile_ShouldWork()
        {
            var outer = new Outer
            {
                Name = "Test",
                InnerObj = new Inner { Value = 42 }
            };

            var filePath = "test_messagepack.bin";

            // ファイルにシリアライズして保存
            File.WriteAllBytes(filePath, MessagePackSerializer.Serialize(outer));

            // ファイルからデシリアライズ
            var bytes = File.ReadAllBytes(filePath);
            var restored = MessagePackSerializer.Deserialize<Outer>(bytes);

            Assert.AreEqual("Test", restored.Name);
            Assert.IsNotNull(restored.InnerObj);
            Assert.AreEqual(42, restored.InnerObj.Value);

            // テスト後にファイル削除
            File.Delete(filePath);
        }

        [Test]
        public void SerializeEncryptToFile_And_DecryptDeserializeFromFile_ShouldWork()
        {
            var outer = new Outer
            {
                Name = "Test",
                InnerObj = new Inner { Value = 42 }
            };

            var filePath = "test_messagepack_encrypted.bin";
            var key = Encoding.UTF8.GetBytes("1234567890123456"); // 16byte
            var iv = Encoding.UTF8.GetBytes("6543210987654321"); // 16byte

            // シリアライズ & 暗号化
            var bytes = MessagePackSerializer.Serialize(outer);
            var encrypted = Encrypt(bytes, key, iv);
            File.WriteAllBytes(filePath, encrypted);

            // 復号化 & デシリアライズ
            var readEncrypted = File.ReadAllBytes(filePath);
            var decrypted = Decrypt(readEncrypted, key, iv);
            var restored = MessagePackSerializer.Deserialize<Outer>(decrypted);

            Assert.AreEqual("Test", restored.Name);
            Assert.IsNotNull(restored.InnerObj);
            Assert.AreEqual(42, restored.InnerObj.Value);

            File.Delete(filePath);
        }

        [Test]
        public void SerializeAndDeserialize_DerivedMessagePackObject_ShouldRestoreAllProperties()
        {
            var obj = new DerivedOuter
            {
                Name = "Taro",
                Age = 30
            };

            var bytes = MessagePackSerializer.Serialize(obj);
            var restored = MessagePackSerializer.Deserialize<DerivedOuter>(bytes);

            Assert.AreEqual("Taro", restored.Name);
            Assert.AreEqual(30, restored.Age);
        }

        private static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            using var encryptor = aes.CreateEncryptor(key, iv);
            return encryptor.TransformFinalBlock(data, 0, data.Length);
        }

        private static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            using var decryptor = aes.CreateDecryptor(key, iv);
            return decryptor.TransformFinalBlock(data, 0, data.Length);
        }
    }
}