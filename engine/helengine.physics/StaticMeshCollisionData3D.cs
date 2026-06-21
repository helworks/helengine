namespace helengine {
    /// <summary>
    /// Stores one cooked static triangle-mesh collision blob in a compact vertex and index form.
    /// </summary>
    public sealed class StaticMeshCollisionData3D {
        /// <summary>
        /// Backing field for the local-space triangle vertices.
        /// </summary>
        float3[] VerticesValue;

        /// <summary>
        /// Backing field for the triangle indices grouped in triples.
        /// </summary>
        int[] IndicesValue;

        /// <summary>
        /// Initializes one empty collision blob for reflected scene-payload materialization.
        /// </summary>
        public StaticMeshCollisionData3D() {
        }

        /// <summary>
        /// Initializes a new static collision data blob.
        /// </summary>
        /// <param name="vertices">Local-space triangle vertices.</param>
        /// <param name="indices">Triangle indices grouped in triples.</param>
        public StaticMeshCollisionData3D(float3[] vertices, int[] indices) {
            Vertices = vertices;
            Indices = indices;
        }

        /// <summary>
        /// Gets the local-space triangle vertices.
        /// </summary>
        public float3[] Vertices {
            get {
                return VerticesValue ?? throw new InvalidOperationException("Static mesh collision vertices must be initialized before use.");
            }
            set {
                VerticesValue = ValidateVertices(value);
                if (IndicesValue != null) {
                    IndicesValue = ValidateIndices(IndicesValue, VerticesValue.Length);
                }
            }
        }

        /// <summary>
        /// Gets the triangle indices grouped in triples.
        /// </summary>
        public int[] Indices {
            get {
                return IndicesValue ?? throw new InvalidOperationException("Static mesh collision indices must be initialized before use.");
            }
            set {
                int[] validatedIndices = ValidateIndexShape(value);
                if (VerticesValue != null) {
                    validatedIndices = ValidateIndices(validatedIndices, VerticesValue.Length);
                }

                IndicesValue = validatedIndices;
            }
        }

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
        static int[] ValidateIndexShape(int[] indices) {
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }
            if (indices.Length < 3 || indices.Length % 3 != 0) {
                throw new ArgumentOutOfRangeException(nameof(indices), "Static mesh collision indices must contain one or more triangles grouped in triples.");
            }

            return indices;
        }

        /// <summary>
        /// Validates the supplied triangle index array against the available vertex count.
        /// </summary>
        /// <param name="indices">Index array being validated.</param>
        /// <param name="vertexCount">Number of available vertices.</param>
        /// <returns>Validated index array.</returns>
        static int[] ValidateIndices(int[] indices, int vertexCount) {
            int[] validatedIndices = ValidateIndexShape(indices);

            for (int index = 0; index < validatedIndices.Length; index++) {
                if (validatedIndices[index] < 0 || validatedIndices[index] >= vertexCount) {
                    throw new ArgumentOutOfRangeException(nameof(indices), "Static mesh collision indices must reference valid vertices.");
                }
            }

            return validatedIndices;
        }
    }
}
