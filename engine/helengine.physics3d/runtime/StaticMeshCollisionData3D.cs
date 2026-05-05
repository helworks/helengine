namespace helengine {
    /// <summary>
    /// Stores one cooked static triangle-mesh collision blob in a compact vertex and index form.
    /// </summary>
    public sealed class StaticMeshCollisionData3D {
        /// <summary>
        /// Initializes a new static collision data blob.
        /// </summary>
        /// <param name="vertices">Local-space triangle vertices.</param>
        /// <param name="indices">Triangle indices grouped in triples.</param>
        public StaticMeshCollisionData3D(float3[] vertices, int[] indices) {
            Vertices = ValidateVertices(vertices);
            Indices = ValidateIndices(indices, Vertices.Length);
        }

        /// <summary>
        /// Gets the local-space triangle vertices.
        /// </summary>
        public float3[] Vertices { get; }

        /// <summary>
        /// Gets the triangle indices grouped in triples.
        /// </summary>
        public int[] Indices { get; }

        /// <summary>
        /// Gets the number of triangles stored in this cooked blob.
        /// </summary>
        public int TriangleCount => Indices.Length / 3;

        /// <summary>
        /// Validates the supplied local-space vertex array.
        /// </summary>
        /// <param name="vertices">Vertex array being validated.</param>
        /// <returns>Validated vertex array.</returns>
        static float3[] ValidateVertices(float3[] vertices) {
            if (vertices == null) {
                throw new ArgumentNullException(nameof(vertices));
            }
            if (vertices.Length < 3) {
                throw new ArgumentOutOfRangeException(nameof(vertices), "Static mesh collision data requires at least three vertices.");
            }

            return vertices;
        }

        /// <summary>
        /// Validates the supplied triangle index array.
        /// </summary>
        /// <param name="indices">Index array being validated.</param>
        /// <param name="vertexCount">Number of available vertices.</param>
        /// <returns>Validated index array.</returns>
        static int[] ValidateIndices(int[] indices, int vertexCount) {
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }
            if (indices.Length < 3 || indices.Length % 3 != 0) {
                throw new ArgumentOutOfRangeException(nameof(indices), "Static mesh collision indices must contain one or more triangles grouped in triples.");
            }

            for (int index = 0; index < indices.Length; index++) {
                if (indices[index] < 0 || indices[index] >= vertexCount) {
                    throw new ArgumentOutOfRangeException(nameof(indices), "Static mesh collision indices must reference valid vertices.");
                }
            }

            return indices;
        }
    }
}
