namespace helengine {
    /// <summary>
    /// Represents raw mesh data including positions, normals, UVs, and indices.
    /// </summary>
    public class ModelAsset : Asset, IDisposable {
        /// <summary>
        /// Tracks whether this raw model has already released its native geometry buffers.
        /// </summary>
        bool IsDisposedValue;

        /// <summary>
        /// Vertex positions.
        /// </summary>
        [NativeOwnedMember]
        public float3[] Positions;

        /// <summary>
        /// Vertex normals.
        /// </summary>
        [NativeOwnedMember]
        public float3[] Normals;

        /// <summary>
        /// Texture coordinates.
        /// </summary>
        [NativeOwnedMember]
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
        [NativeOwnedMember]
        public ushort[] Indices16;

        /// <summary>
        /// Index buffer using 32-bit indices.
        /// </summary>
        [NativeOwnedMember]
        public uint[] Indices32;

        /// <summary>
        /// Authored submesh ranges and their material slot names.
        /// </summary>
        [NativeOwnedMember]
        public ModelSubmeshAsset[] Submeshes;

        /// <summary>
        /// Releases every geometry buffer owned by this raw model, including the authored submesh descriptors.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref Positions);
            NativeOwnership.Release(ref Normals);
            NativeOwnership.Release(ref TexCoords);
            NativeOwnership.Release(ref Indices16);
            NativeOwnership.Release(ref Indices32);
            NativeOwnership.DeleteItemsAndRelease(ref Submeshes);
            IsDisposedValue = true;
        }
    }
}
