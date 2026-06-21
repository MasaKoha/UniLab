using System.Text;
using UnityEngine;

namespace UniLab.Persistence
{
    /// <summary>
    /// Unity 標準の JsonUtility を用いた既定のシリアライザ。
    /// LocalSave / EncryptedLocalStorage の初期値として使われ、従来挙動と後方互換を保つ。
    /// </summary>
    public sealed class JsonLocalSaveSerializer : ILocalSaveSerializer
    {
        /// <inheritdoc/>
        public byte[] Serialize<TData>(TData data)
        {
            var json = JsonUtility.ToJson(data);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <inheritdoc/>
        public TData Deserialize<TData>(byte[] bytes)
        {
            var json = Encoding.UTF8.GetString(bytes);
            return JsonUtility.FromJson<TData>(json);
        }
    }
}
