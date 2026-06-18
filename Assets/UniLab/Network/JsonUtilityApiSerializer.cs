using System.Text;
using UnityEngine;

namespace UniLab.Network
{
    /// <summary>
    /// Default <see cref="IApiSerializer"/> implementation backed by <see cref="JsonUtility"/>.
    /// Encodes and decodes UTF-8 JSON without a BOM.
    /// </summary>
    public sealed class JsonUtilityApiSerializer : IApiSerializer
    {
        /// <summary>
        /// Returns <c>"application/json"</c>.
        /// </summary>
        public string ContentType => "application/json";

        /// <summary>
        /// Serializes <paramref name="value"/> to a UTF-8 JSON byte array without a BOM.
        /// </summary>
        public byte[] Serialize<T>(T value)
        {
            var json = JsonUtility.ToJson(value);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Deserializes a UTF-8 JSON byte array into <typeparamref name="T"/>.
        /// Returns <c>null</c> (or the default value for value types) when <paramref name="body"/> is empty.
        /// </summary>
        public T Deserialize<T>(byte[] body)
        {
            if (body == null || body.Length == 0)
            {
                return default;
            }

            var json = Encoding.UTF8.GetString(body);
            return JsonUtility.FromJson<T>(json);
        }
    }
}
