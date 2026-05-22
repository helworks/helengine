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
        /// Minimum authored vertex position used by preview and framing code when the import pipeline preserves bounds.
        /// </summary>
        public float3 BoundsMin;

        /// <summary>
        /// Maximum authored vertex position used by preview and framing code when the import pipeline preserves bounds.
        /// </summary>
        public float3 BoundsMax;

        /// <summary>
        /// Index buffer using 16-bit indices.
        /// </summary>
        public ushort[] Indices16;

        /// <summary>
        /// Index buffer using 32-bit indices.
        /// </summary>
        public uint[] Indices32;

        /// <summary>
        /// Authored submesh ranges and their material slot names.
        /// </summary>
        public ModelSubmeshAsset[] Submeshes;

    }
}
