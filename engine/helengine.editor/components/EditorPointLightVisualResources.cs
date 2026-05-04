using System.Collections.Generic;

namespace helengine {
    /// <summary>
    /// Builds and caches the shared runtime resources used by editor-only point light visuals.
    /// </summary>
    public static class EditorPointLightVisualResources {
        /// <summary>
        /// Radius of the editor point-light sphere.
        /// </summary>
        const float SphereRadius = 0.24f;
        /// <summary>
        /// Radius of the short stem cylinder.
        /// </summary>
        const float StemRadius = 0.06f;
        /// <summary>
        /// Length of the short stem cylinder.
        /// </summary>
        const float StemLength = 0.34f;
        /// <summary>
        /// Segment count used for rounded point-light details.
        /// </summary>
        const int RoundSegments = 18;
        /// <summary>
        /// Cached runtime model shared by all editor point-light visuals.
        /// </summary>
        static RuntimeModel RuntimeModelValue;

        /// <summary>
        /// Clears cached runtime resources so tests can start from a known state.
        /// </summary>
        public static void ResetForTests() {
            RuntimeModelValue = null;
        }

        /// <summary>
        /// Gets the shared runtime model used by editor point-light visuals.
        /// </summary>
        /// <returns>Cached runtime model instance.</returns>
        public static RuntimeModel GetRuntimeModel() {
            if (RuntimeModelValue != null) {
                return RuntimeModelValue;
            }

            Core core = Core.Instance;
            if (core == null) {
                throw new InvalidOperationException("Core must be initialized before editor point-light visuals can be built.");
            }
            if (core.RenderManager3D == null) {
                throw new InvalidOperationException("A 3D render manager is required before editor point-light visuals can be built.");
            }

            RuntimeModelValue = core.RenderManager3D.BuildModelFromRaw(CreateModelAsset());
            return RuntimeModelValue;
        }

        /// <summary>
        /// Builds one combined point-light model from a sphere and a short stem cylinder.
        /// </summary>
        /// <returns>Raw model asset representing the editor point-light visual.</returns>
        static ModelAsset CreateModelAsset() {
            List<float3> positions = new List<float3>();
            List<float3> normals = new List<float3>();
            List<float2> texCoords = new List<float2>();
            List<ushort> indices = new List<ushort>();

            AppendMesh(
                positions,
                normals,
                texCoords,
                indices,
                helengine.editor.TransformGizmoMeshFactory.CreateSphere(SphereRadius, RoundSegments),
                float4.Identity,
                float3.Zero);
            AppendMesh(
                positions,
                normals,
                texCoords,
                indices,
                helengine.editor.TransformGizmoMeshFactory.CreateCylinder(StemRadius, StemLength, RoundSegments),
                float4.Identity,
                new float3(0f, -(SphereRadius + StemLength), 0f));

            return new ModelAsset {
                Id = "editor:point-light-visual",
                Positions = positions.ToArray(),
                Normals = normals.ToArray(),
                TexCoords = texCoords.ToArray(),
                Indices16 = indices.ToArray()
            };
        }

        /// <summary>
        /// Appends one transformed source mesh into the supplied combined mesh streams.
        /// </summary>
        /// <param name="positions">Destination position stream.</param>
        /// <param name="normals">Destination normal stream.</param>
        /// <param name="texCoords">Destination UV stream.</param>
        /// <param name="indices">Destination index stream.</param>
        /// <param name="source">Source mesh to append.</param>
        /// <param name="orientation">Orientation applied to positions and normals.</param>
        /// <param name="translation">Translation applied after rotation.</param>
        static void AppendMesh(
            List<float3> positions,
            List<float3> normals,
            List<float2> texCoords,
            List<ushort> indices,
            ModelAsset source,
            float4 orientation,
            float3 translation) {
            if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            }
            if (normals == null) {
                throw new ArgumentNullException(nameof(normals));
            }
            if (texCoords == null) {
                throw new ArgumentNullException(nameof(texCoords));
            }
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }
            if (source.Positions == null || source.Normals == null || source.TexCoords == null || source.Indices16 == null) {
                throw new InvalidOperationException("Editor point-light visual meshes require complete 16-bit vertex data.");
            }

            int vertexOffset = positions.Count;
            if (vertexOffset > ushort.MaxValue) {
                throw new InvalidOperationException("Editor point-light visual vertex count exceeds 16-bit index capacity.");
            }

            for (int i = 0; i < source.Positions.Length; i++) {
                positions.Add(float4.RotateVector(source.Positions[i], orientation) + translation);
                normals.Add(float4.RotateVector(source.Normals[i], orientation));
                texCoords.Add(source.TexCoords[i]);
            }

            for (int i = 0; i < source.Indices16.Length; i++) {
                int combinedIndex = source.Indices16[i] + vertexOffset;
                if (combinedIndex > ushort.MaxValue) {
                    throw new InvalidOperationException("Editor point-light visual index exceeds 16-bit capacity.");
                }

                indices.Add((ushort)combinedIndex);
            }
        }
    }
}
