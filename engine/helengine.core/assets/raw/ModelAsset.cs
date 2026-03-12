namespace helengine {
    /// <summary>
    /// Represents raw mesh data including positions, normals, UVs, and indices.
    /// </summary>
    public class ModelAsset : Asset {
        /// <summary>
        /// Vertex positions.
        /// </summary>
        public float3[] Positions;

        /// <summary>
        /// Vertex normals.
        /// </summary>
        public float3[] Normals;

        /// <summary>
        /// Texture coordinates.
        /// </summary>
        public float2[] TexCoords;

        /// <summary>
        /// Index buffer using 16-bit indices.
        /// </summary>
        public ushort[] Indices16;
    }
}
