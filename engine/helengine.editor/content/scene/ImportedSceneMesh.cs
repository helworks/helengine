namespace helengine.editor {
    /// <summary>
    /// Geometry of one imported mesh in its own local space, with per-vertex streams of equal length.
    /// </summary>
    public sealed class ImportedSceneMesh {
        /// <summary>
        /// Creates the immutable ImportedSceneMesh from already-read data; reference arguments must not be null.
        /// </summary>
        /// <param name="name">The mesh name.</param>
        /// <param name="positions">The vertex positions.</param>
        /// <param name="normals">The vertex normals.</param>
        /// <param name="texCoords">The first UV channel, zero-filled when the source has none.</param>
        /// <param name="tangents">The tangents with the bitangent sign in W; empty when the source provides none.</param>
        /// <param name="indices">The triangle list indices.</param>
        /// <param name="materialIndex">The index into the scene material table.</param>
        public ImportedSceneMesh(string name, float3[] positions, float3[] normals, float2[] texCoords, float4[] tangents, uint[] indices, int materialIndex) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Positions = positions ?? throw new ArgumentNullException(nameof(positions));
            Normals = normals ?? throw new ArgumentNullException(nameof(normals));
            TexCoords = texCoords ?? throw new ArgumentNullException(nameof(texCoords));
            Tangents = tangents ?? throw new ArgumentNullException(nameof(tangents));
            Indices = indices ?? throw new ArgumentNullException(nameof(indices));
            MaterialIndex = materialIndex;
        }

        /// <summary>
        /// Gets the mesh name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the vertex positions.
        /// </summary>
        public float3[] Positions { get; }

        /// <summary>
        /// Gets the vertex normals.
        /// </summary>
        public float3[] Normals { get; }

        /// <summary>
        /// Gets the first UV channel, zero-filled when the source has none.
        /// </summary>
        public float2[] TexCoords { get; }

        /// <summary>
        /// Gets the tangents with the bitangent sign in W; empty when the source provides none.
        /// </summary>
        public float4[] Tangents { get; }

        /// <summary>
        /// Gets the triangle list indices.
        /// </summary>
        public uint[] Indices { get; }

        /// <summary>
        /// Gets the index into the scene material table.
        /// </summary>
        public int MaterialIndex { get; }
    }
}
