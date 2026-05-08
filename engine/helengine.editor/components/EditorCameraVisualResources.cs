using System.Collections.Generic;

namespace helengine {
    /// <summary>
    /// Builds and caches the shared runtime resources used by editor-only camera visuals.
    /// </summary>
    public static class EditorCameraVisualResources {
        /// <summary>
        /// Width of the camera body.
        /// </summary>
        const float BodyWidth = 1.2f;
        /// <summary>
        /// Height of the camera body.
        /// </summary>
        const float BodyHeight = 0.6f;
        /// <summary>
        /// Depth of the camera body.
        /// </summary>
        const float BodyDepth = 0.6f;
        /// <summary>
        /// Radius of each top cylinder.
        /// </summary>
        const float TopCylinderRadius = BodyWidth * 0.15f;
        /// <summary>
        /// Length of each top cylinder.
        /// </summary>
        const float TopCylinderLength = BodyWidth * 0.3f;
        /// <summary>
        /// Radius of the front cone base.
        /// </summary>
        const float FrontConeRadius = 0.375f;
        /// <summary>
        /// Length of the front cone.
        /// </summary>
        const float FrontConeLength = 0.7f;
        /// <summary>
        /// Segment count used for rounded camera details.
        /// </summary>
        const int RoundSegments = 18;
        /// <summary>
        /// Cached runtime model shared by all editor camera visuals.
        /// </summary>
        static RuntimeModel RuntimeModelValue;

        /// <summary>
        /// Clears cached runtime resources so tests can start from a known state.
        /// </summary>
        public static void ResetForTests() {
            RuntimeModelValue = null;
        }

        /// <summary>
        /// Gets the shared runtime model used by editor camera visuals.
        /// </summary>
        /// <returns>Cached runtime model instance.</returns>
        public static RuntimeModel GetRuntimeModel() {
            if (RuntimeModelValue != null) {
                return RuntimeModelValue;
            }

            Core core = Core.Instance;
            if (core == null) {
                throw new InvalidOperationException("Core must be initialized before editor camera visuals can be built.");
            }
            if (core.RenderManager3D == null) {
                throw new InvalidOperationException("A 3D render manager is required before editor camera visuals can be built.");
            }

            RuntimeModelValue = core.RenderManager3D.BuildModelFromRaw(CreateModelAsset());
            return RuntimeModelValue;
        }

        /// <summary>
        /// Builds one combined camera-icon model from a body, two top rollers, and a forward-facing lens cone.
        /// </summary>
        /// <returns>Raw model asset representing the editor camera visual.</returns>
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
                helengine.editor.TransformGizmoMeshFactory.CreateBox(BodyDepth, BodyHeight, BodyWidth),
                float4.Identity,
                new float3(0f, -BodyHeight * 0.5f, 0f));
            AppendMesh(
                positions,
                normals,
                texCoords,
                indices,
                helengine.editor.TransformGizmoMeshFactory.CreateCylinder(TopCylinderRadius, TopCylinderLength, RoundSegments),
                CreateTopCylinderOrientation(),
                new float3(TopCylinderLength * 0.5f, GetBodyTopY() + TopCylinderRadius, -TopCylinderRadius));
            AppendMesh(
                positions,
                normals,
                texCoords,
                indices,
                helengine.editor.TransformGizmoMeshFactory.CreateCylinder(TopCylinderRadius, TopCylinderLength, RoundSegments),
                CreateTopCylinderOrientation(),
                new float3(TopCylinderLength * 0.5f, GetBodyTopY() + TopCylinderRadius, TopCylinderRadius));
            AppendMesh(
                positions,
                normals,
                texCoords,
                indices,
                helengine.editor.TransformGizmoMeshFactory.CreateCone(FrontConeRadius, FrontConeLength, RoundSegments),
                CreateLensOrientation(),
                new float3(0f, 0f, -(BodyWidth * 0.5f) - (FrontConeLength * 0.85f)));

            return new ModelAsset {
                Id = "editor:camera-visual",
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
                throw new InvalidOperationException("Editor camera visual meshes require complete 16-bit vertex data.");
            }

            int vertexOffset = positions.Count;
            if (vertexOffset > ushort.MaxValue) {
                throw new InvalidOperationException("Editor camera visual vertex count exceeds 16-bit index capacity.");
            }

            for (int i = 0; i < source.Positions.Length; i++) {
                positions.Add(float4.RotateVector(source.Positions[i], orientation) + translation);
                normals.Add(float4.RotateVector(source.Normals[i], orientation));
                texCoords.Add(source.TexCoords[i]);
            }

            for (int i = 0; i < source.Indices16.Length; i++) {
                int combinedIndex = source.Indices16[i] + vertexOffset;
                if (combinedIndex > ushort.MaxValue) {
                    throw new InvalidOperationException("Editor camera visual index exceeds 16-bit capacity.");
                }

                indices.Add((ushort)combinedIndex);
            }
        }

        /// <summary>
        /// Creates the rotation that maps +Y-oriented primitives into the local +Z axis.
        /// </summary>
        /// <returns>Quaternion rotating +Y into +Z.</returns>
        static float4 CreatePositiveZAxisOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the top-cylinder rotation that lays each reel across the top of the camera body.
        /// </summary>
        /// <returns>Quaternion rotating the top cylinders 90 degrees around local Z.</returns>
        static float4 CreateTopCylinderOrientation() {
            float3 zAxis = new float3(0f, 0f, 1f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref zAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the lens rotation by mapping +Y into +Z.
        /// </summary>
        /// <returns>Quaternion rotating the cone into the camera front axis.</returns>
        static float4 CreateLensOrientation() {
            return CreatePositiveZAxisOrientation();
        }

        /// <summary>
        /// Gets half of the camera body width used to place details against the front and back faces.
        /// </summary>
        /// <returns>Positive half-width of the camera body along local Z.</returns>
        static float GetBodyHalfWidth() {
            return BodyWidth * 0.5f;
        }

        /// <summary>
        /// Gets the local-space Y position of the top face of the camera body.
        /// </summary>
        /// <returns>Top-face Y coordinate for the translated camera body.</returns>
        static float GetBodyTopY() {
            return BodyHeight * 0.5f;
        }
    }
}
