using System;
using System.Security.Cryptography;
using MessagePack;
using UniLab.Common.Utility;

namespace UniLab.Feature.MasterData
{
    public static class MasterCalculator
    {
        public static string CalculateMasterBinaryHash<TMaster>(TMaster master, byte[] key, byte[] iv) where TMaster : MasterBase
        {
            if (master == null)
            {
                throw new ArgumentNullException(nameof(master));
            }

            var serialized = MessagePackSerializer.Serialize(master);
            var encrypted = AesEncryptionUtility.Encrypt(serialized, key, iv);
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(encrypted));
        }
    }
}