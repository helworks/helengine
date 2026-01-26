namespace helengine {
    /// <summary>
    /// Base asset type containing a unique identifier.
    /// </summary>
    [ProtoBuf.ProtoContract]
    [ProtoBuf.ProtoInclude(100, typeof(TextureAsset))]
    [ProtoBuf.ProtoInclude(101, typeof(ModelAsset))]
    [ProtoBuf.ProtoInclude(102, typeof(ShaderAsset))]
    [ProtoBuf.ProtoInclude(103, typeof(TextAsset))]
    [ProtoBuf.ProtoInclude(104, typeof(MaterialAsset))]
    public class Asset {
        /// <summary>
        /// Gets or sets the asset identifier.
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public string Id { get; set; }
    }
}
