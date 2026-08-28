namespace helengine.editor {
    /// <summary>
    /// Builds and caches the shared corner-origin XY-plane mesh used by editor viewport border gizmos.
    /// </summary>
    public sealed class EditorViewportBorderGizmoMeshResources : IDisposable {
        /// <summary>
        /// Stable raw asset id used by the shared authored-viewport gizmo plane.
        /// </summary>
        const string UnitQuadAssetId = "editor:viewport-border-gizmo-unit-quad";

        /// <summary>
        /// Cached runtime model for the shared authored-viewport gizmo plane.
        /// </summary>
        readonly RenderManager3D RenderManager3D;
        RuntimeModel RuntimeModelValue;
        bool IsDisposed;

        /// <summary>
        /// Initializes one viewport border mesh resource owner for one renderer.
        /// </summary>
        public EditorViewportBorderGizmoMeshResources(RenderManager3D renderManager3D) {
            RenderManager3D = renderManager3D ?? throw new ArgumentNullException(nameof(renderManager3D));
        }

        /// <summary>
        /// Gets the shared corner-origin XY-plane runtime model used by authored viewport border gizmos.
        /// </summary>
        /// <returns>Shared runtime model for authored viewport border gizmos.</returns>
        public RuntimeModel GetRuntimeModel() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorViewportBorderGizmoMeshResources));
            }
            if (RuntimeModelValue == null) {
                RuntimeModelValue = RenderManager3D.BuildModelFromRaw(CreateModelAsset());
            }
            return RuntimeModelValue;
        }

        /// <summary>Releases the renderer-owned viewport border model.</summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }
            RuntimeModelValue?.Dispose();
            RuntimeModelValue = null;
            IsDisposed = true;
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
