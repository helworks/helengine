namespace helengine {
    /// <summary>
    /// Represents raw mesh data including positions, normals, UVs, and indices.
    /// </summary>
    [ProtoBuf.ProtoContract]
    public class ModelAsset : Asset {
        /// <summary>
        /// Vertex positions.
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public float3[] Positions;

        /// <summary>
        /// Vertex normals.
        /// </summary>
        [ProtoBuf.ProtoMember(2)]
        public float3[] Normals;

        /// <summary>
        /// Texture coordinates.
        /// </summary>
        [ProtoBuf.ProtoMember(3)]
        public float2[] TexCoords;

        /// <summary>
        /// Index buffer using 16-bit indices.
        /// </summary>
        [ProtoBuf.ProtoMember(4)]
        public ushort[] Indices16;
    }
}
