namespace helengine {
    /// <summary>
    /// Builds and caches the shared unit quad used by editor-only world-space 2D preview proxies.
    /// </summary>
    public static class EditorWorldSpace2DPreviewMeshResources {
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
        static RuntimeModel RuntimeModelValue;
        /// <summary>
        /// Cached runtime model for the shared viewport-space unit quad.
        /// </summary>
        static RuntimeModel ViewportRuntimeModelValue;
        /// <summary>
        /// Cached runtime model for the shared viewport-space unit quad used by render-target-backed previews.
        /// </summary>
        static RuntimeModel ViewportRenderTargetRuntimeModelValue;

        /// <summary>
        /// Clears cached preview-mesh resources so tests can start from a known state.
        /// </summary>
        public static void ResetForTests() {
            RuntimeModelValue = null;
            ViewportRuntimeModelValue = null;
            ViewportRenderTargetRuntimeModelValue = null;
        }

        /// <summary>
        /// Gets the shared unit quad runtime model used by world-space 2D preview proxies.
        /// </summary>
        /// <returns>Shared runtime model for preview proxies.</returns>
        public static RuntimeModel GetRuntimeModel() {
            if (RuntimeModelValue != null) {
                return RuntimeModelValue;
            }

            Core core = Core.Instance;
            if (core == null) {
                throw new InvalidOperationException("Core must be initialized before editor world-space 2D preview meshes can be built.");
            } else if (core.RenderManager3D == null) {
                throw new InvalidOperationException("A 3D render manager is required before editor world-space 2D preview meshes can be built.");
            }

            RuntimeModelValue = core.RenderManager3D.BuildModelFromRaw(CreateModelAsset());
            return RuntimeModelValue;
        }

        /// <summary>
        /// Gets the shared viewport-space unit quad runtime model used by viewport-authored world-space 2D preview proxies.
        /// </summary>
        /// <returns>Shared runtime model whose local rectangle spans positive X and negative Y from the authored entity origin.</returns>
        public static RuntimeModel GetViewportRuntimeModel() {
            if (ViewportRuntimeModelValue != null) {
                return ViewportRuntimeModelValue;
            }

            Core core = Core.Instance;
            if (core == null) {
                throw new InvalidOperationException("Core must be initialized before editor world-space 2D preview meshes can be built.");
            } else if (core.RenderManager3D == null) {
                throw new InvalidOperationException("A 3D render manager is required before editor world-space 2D preview meshes can be built.");
            }

            ViewportRuntimeModelValue = core.RenderManager3D.BuildModelFromRaw(CreateViewportModelAsset());
            return ViewportRuntimeModelValue;
        }

        /// <summary>
        /// Gets the shared viewport-space unit quad runtime model used by viewport-authored render-target-backed world-space 2D preview proxies.
        /// </summary>
        /// <returns>Shared runtime model whose local rectangle spans positive X and negative Y while preserving render-target top-edge sampling.</returns>
        public static RuntimeModel GetViewportRenderTargetRuntimeModel() {
            if (ViewportRenderTargetRuntimeModelValue != null) {
                return ViewportRenderTargetRuntimeModelValue;
            }

            Core core = Core.Instance;
            if (core == null) {
                throw new InvalidOperationException("Core must be initialized before editor world-space 2D preview meshes can be built.");
            } else if (core.RenderManager3D == null) {
                throw new InvalidOperationException("A 3D render manager is required before editor world-space 2D preview meshes can be built.");
            }

            ViewportRenderTargetRuntimeModelValue = core.RenderManager3D.BuildModelFromRaw(CreateViewportRenderTargetModelAsset());
            return ViewportRenderTargetRuntimeModelValue;
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
