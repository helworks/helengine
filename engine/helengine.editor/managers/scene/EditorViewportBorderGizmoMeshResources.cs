namespace helengine.editor {
    /// <summary>
    /// Builds and caches the shared corner-origin XY-plane mesh used by editor viewport border gizmos.
    /// </summary>
    public static class EditorViewportBorderGizmoMeshResources {
        /// <summary>
        /// Stable raw asset id used by the shared authored-viewport gizmo plane.
        /// </summary>
        const string UnitQuadAssetId = "editor:viewport-border-gizmo-unit-quad";

        /// <summary>
        /// Cached runtime model for the shared authored-viewport gizmo plane.
        /// </summary>
        static RuntimeModel RuntimeModelValue;

        /// <summary>
        /// Clears cached gizmo mesh resources so tests can start from a known state.
        /// </summary>
        public static void ResetForTests() {
            RuntimeModelValue = null;
        }

        /// <summary>
        /// Gets the shared corner-origin XY-plane runtime model used by authored viewport border gizmos.
        /// </summary>
        /// <returns>Shared runtime model for authored viewport border gizmos.</returns>
        public static RuntimeModel GetRuntimeModel() {
            if (RuntimeModelValue != null) {
                return RuntimeModelValue;
            }

            Core core = Core.Instance;
            if (core == null) {
                throw new InvalidOperationException("Core must be initialized before editor viewport border meshes can be built.");
            } else if (core.RenderManager3D == null) {
                throw new InvalidOperationException("A 3D render manager is required before editor viewport border meshes can be built.");
            }

            RuntimeModelValue = core.RenderManager3D.BuildModelFromRaw(CreateModelAsset());
            return RuntimeModelValue;
        }

        /// <summary>
        /// Builds the corner-origin XY-plane mesh used by authored viewport border gizmos.
        /// </summary>
        /// <returns>Model asset whose UVs span the full local rectangle.</returns>
        static ModelAsset CreateModelAsset() {
            return new ModelAsset {
                Id = UnitQuadAssetId,
                Positions = [
                    new float3(0f, 0f, 0f),
                    new float3(1f, 0f, 0f),
                    new float3(1f, -1f, 0f),
                    new float3(0f, -1f, 0f),
                    new float3(0f, 0f, 0f),
                    new float3(0f, -1f, 0f),
                    new float3(1f, -1f, 0f),
                    new float3(1f, 0f, 0f)
                ],
                Normals = [
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, -1f),
                    new float3(0f, 0f, -1f),
                    new float3(0f, 0f, -1f),
                    new float3(0f, 0f, -1f)
                ],
                TexCoords = [
                    new float2(0f, 1f),
                    new float2(1f, 1f),
                    new float2(1f, 0f),
                    new float2(0f, 0f),
                    new float2(0f, 1f),
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(1f, 1f)
                ],
                Indices16 = [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7]
            };
        }
    }
}
