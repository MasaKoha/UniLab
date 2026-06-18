namespace UniLab.Network
{
    /// <summary>
    /// Abstracts HTTP body serialization and deserialization.
    /// Implement this interface to plug in alternative serializers such as Newtonsoft.Json,
    /// MessagePack, or MemoryPack without modifying <see cref="ApiClientBase"/>.
    /// </summary>
    public interface IApiSerializer
    {
        /// <summary>
        /// MIME type sent as both the <c>Content-Type</c> and <c>Accept</c> HTTP headers.
        /// Example: <c>"application/json"</c>, <c>"application/x-msgpack"</c>.
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// Encodes <paramref name="value"/> into the raw bytes that will be sent as the HTTP request body.
        /// </summary>
        byte[] Serialize<T>(T value);

        /// <summary>
        /// Decodes <paramref name="body"/> received from the HTTP response into <typeparamref name="T"/>.
        /// </summary>
        T Deserialize<T>(byte[] body);
    }
}
