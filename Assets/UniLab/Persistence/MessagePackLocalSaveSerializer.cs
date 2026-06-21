using MessagePack;

namespace UniLab.Persistence
{
    /// <summary>
    /// MessagePack for C# を用いたシリアライザ。
    /// 対象型には [MessagePackObject] と [Key] を付与しておく必要がある（属性ベース）。
    /// IL2CPP / AOT ビルドでは、別途 mpc で生成したリゾルバを options 経由で渡すこと
    /// （StandardResolver の動的生成は IL2CPP では動かないため）。
    /// </summary>
    public sealed class MessagePackLocalSaveSerializer : ILocalSaveSerializer
    {
        private readonly MessagePackSerializerOptions _options;

        /// <summary>
        /// options 省略時は MessagePackSerializer.DefaultOptions（StandardResolver）を使う。
        /// LZ4 圧縮や生成済みリゾルバを使いたい場合は options を渡す。
        /// </summary>
        public MessagePackLocalSaveSerializer(MessagePackSerializerOptions options = null)
        {
            _options = options ?? MessagePackSerializer.DefaultOptions;
        }

        /// <inheritdoc/>
        public byte[] Serialize<TData>(TData data)
        {
            return MessagePackSerializer.Serialize(data, _options);
        }

        /// <inheritdoc/>
        public TData Deserialize<TData>(byte[] bytes)
        {
            return MessagePackSerializer.Deserialize<TData>(bytes, _options);
        }
    }
}
