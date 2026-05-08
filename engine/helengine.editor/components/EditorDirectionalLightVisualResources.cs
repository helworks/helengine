using System.Collections.Generic;

namespace helengine {
    /// <summary>
    /// Builds and caches the shared runtime resources used by editor-only directional light visuals.
    /// </summary>
    public static class EditorDirectionalLightVisualResources {
        /// <summary>
        /// Radius of the directional-light shaft.
        /// </summary>
        const float ShaftRadius = 0.05f;
        /// <summary>
        /// Length of the directional-light shaft.
        /// </summary>
        const float ShaftLength = 0.58f;
        /// <summary>
        /// Radius of the directional-light arrow head.
        /// </summary>
        const float HeadRadius = 0.18f;
        /// <summary>
        /// Length of the directional-light arrow head.
        /// </summary>
        const float HeadLength = 0.28f;
        /// <summary>
        /// Segment count used for rounded directional-light details.
        /// </summary>
        const int RoundSegments = 18;
        /// <summary>
        /// Cached runtime model shared by all editor directional-light visuals.
        /// </summary>
        static RuntimeModel RuntimeModelValue;

        /// <summary>
        /// Clears cached runtime resources so tests can start from a known state.
        /// </summary>
        public static void ResetForTests() {
            RuntimeModelValue = null;
        }

        /// <summary>
        /// Gets the shared runtime model used by editor directional-light visuals.
        /// </summary>
        /// <returns>Cached runtime model instance.</returns>
        public static RuntimeModel GetRuntimeModel() {
            if (RuntimeModelValue != null) {
                return RuntimeModelValue;
            }

            Core core = Core.Instance;
            if (core == null) {
                throw new InvalidOperationException("Core must be initialized before editor directional-light visuals can be built.");
            }
            if (core.RenderManager3D == null) {
                throw new InvalidOperationException("A 3D render manager is required before editor directional-light visuals can be built.");
            }

            RuntimeModelValue = core.RenderManager3D.BuildModelFromRaw(CreateModelAsset());
            return RuntimeModelValue;
        }

        /// <summary>
        /// Builds one combined directional-light model from a shaft cylinder and a forward arrow head.
        /// </summary>
        /// <returns>Raw model asset representing the editor directional-light visual.</returns>
        static ModelAsset CreateModelAsset() {
            List<float3> positions = new List<float3>();
            List<float3> normals = new List<float3>();
            List<float2> texCoords = new List<float2>();
            List<ushort> indices = new List<ushort>();

            float4 forwardOrientation = CreateNegativeZAxisOrientation();
            AppendMesh(
                positions,
                normals,
                texCoords,
                indices,
                helengine.editor.TransformGizmoMeshFactory.CreateCylinder(ShaftRadius, ShaftLength, RoundSegments),
                forwardOrientation,
                float3.Zero);
            AppendMesh(
                positions,
                normals,
                texCoords,
                indices,
                helengine.editor.TransformGizmoMeshFactory.CreateCone(HeadRadius, HeadLength, RoundSegments),
                forwardOrientation,
                new float3(0f, 0f, -ShaftLength));

            ModelAsset modelAsset = new ModelAsset {
                Id = "editor:directional-light-visual",
                Positions = positions.ToArray(),
                Normals = normals.ToArray(),
                TexCoords = texCoords.ToArray(),
                Indices16 = indices.ToArray()
            };
            ModelAssetBounds.Apply(modelAsset);
            return modelAsset;
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
                throw new InvalidOperationException("Editor directional-light visual meshes require complete 16-bit vertex data.");
            }

            int vertexOffset = positions.Count;
            if (vertexOffset > ushort.MaxValue) {
                throw new InvalidOperationException("Editor directional-light visual vertex count exceeds 16-bit index capacity.");
            }

            for (int i = 0; i < source.Positions.Length; i++) {
                positions.Add(float4.RotateVector(source.Positions[i], orientation) + translation);
                normals.Add(float4.RotateVector(source.Normals[i], orientation));
                texCoords.Add(source.TexCoords[i]);
            }

            for (int i = 0; i < source.Indices16.Length; i++) {
                int combinedIndex = source.Indices16[i] + vertexOffset;
                if (combinedIndex > ushort.MaxValue) {
                    throw new InvalidOperationException("Editor directional-light visual index exceeds 16-bit capacity.");
                }

                indices.Add((ushort)combinedIndex);
            }
        }

        /// <summary>
        /// Creates the rotation that maps +Y-oriented primitives into the local -Z forward axis.
        /// </summary>
        /// <returns>Quaternion rotating +Y into -Z.</returns>
        static float4 CreateNegativeZAxisOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }
    }
}
