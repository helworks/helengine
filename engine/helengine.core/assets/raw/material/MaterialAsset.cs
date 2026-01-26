namespace helengine {
    /// <summary>
    /// Represents a serialized material definition that references a shader asset.
    /// </summary>
    [ProtoBuf.ProtoContract]
    public class MaterialAsset : Asset {
        /// <summary>
        /// Gets or sets the asset identifier for the shader used by this material.
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public string ShaderAssetId;

        /// <summary>
        /// Gets or sets the vertex program name used by this material.
        /// </summary>
        [ProtoBuf.ProtoMember(2)]
        public string VertexProgram;

        /// <summary>
        /// Gets or sets the pixel program name used by this material.
        /// </summary>
        [ProtoBuf.ProtoMember(3)]
        public string PixelProgram;

        /// <summary>
        /// Gets or sets the shader variant name used by this material.
        /// </summary>
        [ProtoBuf.ProtoMember(4)]
        public string Variant;
    }
}
