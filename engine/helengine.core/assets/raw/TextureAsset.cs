namespace helengine {
    /// <summary>
    /// Represents raw texture data stored in memory.
    /// </summary>
    [ProtoBuf.ProtoContract]
    public class TextureAsset : Asset {
        /// <summary>
        /// Raw color data for the texture in RGBA order.
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public byte[] Colors;

        /// <summary>
        /// Width of the texture in pixels.
        /// </summary>
        [ProtoBuf.ProtoMember(2)]
        public ushort Width;

        /// <summary>
        /// Height of the texture in pixels.
        /// </summary>
        [ProtoBuf.ProtoMember(3)]
        public ushort Height;
    }
}
