using System.Text;
using MessagePack;
using NUnit.Framework;
using UniLab.Common.Utility;
using UniLab.Feature.MasterData;

namespace Sample.Tests
{
    [MessagePackObject]
    public class ItemMaster : MasterBase
    {
        [MessagePackObject]
        public class Record
        {
            [Key("Id")] public int Id;
            [Key("item_name")] public string ItemName;
            [Key("price")] public int Price;
        }

        [Key("records")] public Record[] Records { get; set; }

        public ItemMaster()
        {
            MasterId = GetType().Name;
            Hash = "1234567890abcdef";
        }
    }

    public class ItemMasterTests
    {
        [Test]
        public void VerifyInitialValues()
        {
            var master = new ItemMaster();
            Assert.AreEqual(master.GetType().Name, master.MasterId);
            Assert.AreEqual("1234567890abcdef", master.Hash);
            Assert.IsNull(master.Records);
        }

        [Test]
        public void AddAndRetrieveRecords()
        {
            var master = new ItemMaster
            {
                Records = new[]
                {
                    new ItemMaster.Record { Id = 1, ItemName = "Sword", Price = 100 },
                    new ItemMaster.Record { Id = 2, ItemName = "Shield", Price = 150 }
                }
            };

            Assert.AreEqual(2, master.Records.Length);
            Assert.AreEqual("Sword", master.Records[0].ItemName);
            Assert.AreEqual(150, master.Records[1].Price);
        }

        [Test]
        public void EncryptAndDecryptRecords_ShouldRestoreOriginalData()
        {
            var key = Encoding.UTF8.GetBytes("1234567890123456"); // 16byte
            var iv = Encoding.UTF8.GetBytes("abcdef9876543210"); // 16byte

            var master = new ItemMaster
            {
                Records = new[]
                {
                    new ItemMaster.Record { Id = 1, ItemName = "Sword", Price = 100 },
                    new ItemMaster.Record { Id = 2, ItemName = "Shield", Price = 150 }
                }
            };

            // 暗号化
            var bytes1 = MessagePackSerializer.Serialize(master);
            var encrypted = AesEncryptionUtility.Encrypt(bytes1, key, iv);
            Assert.IsNotNull(encrypted);
            Assert.AreNotEqual("", encrypted);

            // 復号化
            var decrypted = AesEncryptionUtility.Decrypt(encrypted, key, iv);
            var bytes2 = MessagePackSerializer.Deserialize<ItemMaster>(decrypted);
            Assert.IsNotNull(decrypted);
            Assert.AreEqual(master.MasterId, bytes2.MasterId);
            Assert.AreEqual(master.Hash, bytes2.Hash);
            Assert.AreEqual(master.Records.Length, bytes2.Records.Length);
            Assert.AreEqual(master.Records[0].ItemName, bytes2.Records[0].ItemName);
            Assert.AreEqual(master.Records[1].Price, bytes2.Records[1].Price);
        }
    }
}