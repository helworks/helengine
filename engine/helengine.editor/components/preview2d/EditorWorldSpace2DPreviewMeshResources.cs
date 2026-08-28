namespace helengine {
    /// <summary>
    /// Builds and caches the shared unit quad used by editor-only world-space 2D preview proxies.
    /// </summary>
    public sealed class EditorWorldSpace2DPreviewMeshResources : IDisposable {
        /// <summary>
        /// Stable raw asset id used by the shared unit quad.
        /// </summary>
        const string UnitQuadAssetId = "editor:world-space-2d-preview-unit-quad";
        /// <summary>
        /// Stable raw asset id used by the shared viewport-space unit quad.
        /// </summary>
        const string ViewportUnitQuadAssetId = "editor:world-space-2d-preview-viewport-unit-quad";
        /// <summary>
        /// Stable raw asset id used by the shared viewport-space unit quad for render-target-backed previews.
        /// </summary>
        const string ViewportRenderTargetUnitQuadAssetId = "editor:world-space-2d-preview-viewport-render-target-unit-quad";

        /// <summary>
        /// Cached runtime model for the shared unit quad.
        /// </summary>
        readonly RenderManager3D RenderManager3D;
        RuntimeModel RuntimeModelValue;
        /// <summary>
        /// Cached runtime model for the shared viewport-space unit quad.
        /// </summary>
        RuntimeModel ViewportRuntimeModelValue;
        /// <summary>
        /// Cached runtime model for the shared viewport-space unit quad used by render-target-backed previews.
        /// </summary>
        RuntimeModel ViewportRenderTargetRuntimeModelValue;
        bool IsDisposed;

        /// <summary>
        /// Initializes one world-space preview mesh resource owner for one renderer.
        /// </summary>
        public EditorWorldSpace2DPreviewMeshResources(RenderManager3D renderManager3D) {
            RenderManager3D = renderManager3D ?? throw new ArgumentNullException(nameof(renderManager3D));
        }

        /// <summary>
        /// Gets the shared unit quad runtime model used by world-space 2D preview proxies.
        /// </summary>
        /// <returns>Shared runtime model for preview proxies.</returns>
        public RuntimeModel GetRuntimeModel() {
            EnsureNotDisposed();
            if (RuntimeModelValue == null) {
                RuntimeModelValue = RenderManager3D.BuildModelFromRaw(CreateModelAsset());
            }
            return RuntimeModelValue;
        }

        /// <summary>
        /// Gets the shared viewport-space unit quad runtime model used by viewport-authored world-space 2D preview proxies.
        /// </summary>
        /// <returns>Shared runtime model whose local rectangle spans positive X and negative Y from the authored entity origin.</returns>
        public RuntimeModel GetViewportRuntimeModel() {
            EnsureNotDisposed();
            if (ViewportRuntimeModelValue == null) {
                ViewportRuntimeModelValue = RenderManager3D.BuildModelFromRaw(CreateViewportModelAsset());
            }
            return ViewportRuntimeModelValue;
        }

        /// <summary>
        /// Gets the shared viewport-space unit quad runtime model used by viewport-authored render-target-backed world-space 2D preview proxies.
        /// </summary>
        /// <returns>Shared runtime model whose local rectangle spans positive X and negative Y while preserving render-target top-edge sampling.</returns>
        public RuntimeModel GetViewportRenderTargetRuntimeModel() {
            EnsureNotDisposed();
            if (ViewportRenderTargetRuntimeModelValue == null) {
                ViewportRenderTargetRuntimeModelValue = RenderManager3D.BuildModelFromRaw(CreateViewportRenderTargetModelAsset());
            }
            return ViewportRenderTargetRuntimeModelValue;
        }

        /// <summary>Releases all renderer-owned preview mesh models.</summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }
            RuntimeModelValue?.Dispose();
            ViewportRuntimeModelValue?.Dispose();
            ViewportRenderTargetRuntimeModelValue?.Dispose();
            RuntimeModelValue = null;
            ViewportRuntimeModelValue = null;
            ViewportRenderTargetRuntimeModelValue = null;
            IsDisposed = true;
        }

        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorWorldSpace2DPreviewMeshResources));
            }
        }

        /// <summary>
        /// Builds the corner-origin XY-plane mesh used by world-space 2D preview proxies.
        /// </summary>
        /// <returns>Model asset whose local rectangle spans positive X/Y from the authored entity origin.</returns>
        static ModelAsset CreateModelAsset() {
            return new ModelAsset {
                Id = UnitQuadAssetId,
                Positions = [
                    new float3(0f, 0f, 0f),
                    new float3(1f, 0f, 0f),
                    new float3(1f, 1f, 0f),
                    new float3(0f, 1f, 0f),
                    new float3(0f, 0f, 0f),
                    new float3(0f, 1f, 0f),
                    new float3(1f, 1f, 0f),
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

        /// <summary>
        /// Builds the corner-origin X-positive, Y-negative plane mesh used by viewport-authored sprite previews that sample regular texture resources.
        /// </summary>
        /// <returns>Model asset whose local rectangle spans positive X and negative Y from the authored entity origin while keeping regular textures upright.</returns>
        static ModelAsset CreateViewportModelAsset() {
            return new ModelAsset {
                Id = ViewportUnitQuadAssetId,
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
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(1f, 1f),
                    new float2(0f, 1f),
                    new float2(0f, 0f),
                    new float2(0f, 1f),
                    new float2(1f, 1f),
                    new float2(1f, 0f)
                ],
                Indices16 = [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7]
            };
        }

        /// <summary>
        /// Builds the corner-origin X-positive, Y-negative plane mesh used by viewport-authored exact previews that sample render targets.
        /// </summary>
        /// <returns>Model asset whose local rectangle spans positive X and negative Y from the authored entity origin while keeping render-target captures upright.</returns>
        static ModelAsset CreateViewportRenderTargetModelAsset() {
            return new ModelAsset {
                Id = ViewportRenderTargetUnitQuadAssetId,
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
