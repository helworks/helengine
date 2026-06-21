namespace helengine.editor {
    /// <summary>
    /// Creates the internal world-space plane used to display the simulated 2D canvas inside the scene viewport.
    /// </summary>
    public static class EditorViewportCanvasPlaneFactory {
        /// <summary>
        /// Display name assigned to the internal viewport canvas-plane entity.
        /// </summary>
        const string CanvasPlaneEntityName = "Viewport Canvas Plane";
        /// <summary>
        /// Side length of the normalized square mesh used by the canvas plane before transform scaling is applied.
        /// </summary>
        const float CanvasPlaneMeshSize = 1f;

        /// <summary>
        /// Creates the internal world-space plane entity used by the scene viewport canvas preview.
        /// </summary>
        /// <param name="render3D">Renderer used to build runtime mesh and material resources.</param>
        /// <param name="canvasTexture">Sampleable render target texture displayed on the plane.</param>
        /// <returns>Configured internal viewport canvas-plane entity.</returns>
        public static EditorEntity Create(RenderManager3D render3D, RuntimeTexture canvasTexture) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (canvasTexture == null) {
                throw new ArgumentNullException(nameof(canvasTexture));
            }

            RuntimeModel planeModel = render3D.BuildModelFromRaw(CreateCanvasPlaneModelAsset());
            RuntimeMaterial planeMaterial = EditorViewportCanvasPlaneMaterialFactory.Create(render3D, canvasTexture);
            var planeEntity = new EditorEntity {
                Name = CanvasPlaneEntityName,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneCanvasPlane,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            var meshComponent = new MeshComponent {
                Model = planeModel,
                Materials = new[] { planeMaterial }
            };
            planeEntity.AddComponent(meshComponent);
            return planeEntity;
        }

        /// <summary>
        /// Builds the centered plane mesh used by the viewport canvas preview with UVs aligned to top-left 2D canvas coordinates.
        /// </summary>
        /// <returns>Model asset whose top edge samples the top of the preview render target.</returns>
        static ModelAsset CreateCanvasPlaneModelAsset() {
            float halfSize = CanvasPlaneMeshSize * 0.5f;
            return new ModelAsset {
                Positions = [
                    new float3(-halfSize, -halfSize, 0f),
                    new float3(halfSize, -halfSize, 0f),
                    new float3(halfSize, halfSize, 0f),
                    new float3(-halfSize, halfSize, 0f),
                    new float3(-halfSize, -halfSize, 0f),
                    new float3(-halfSize, halfSize, 0f),
                    new float3(halfSize, halfSize, 0f),
                    new float3(halfSize, -halfSize, 0f)
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
                Indices16 = [
                    0, 1, 2,
                    0, 2, 3,
                    4, 5, 6,
                    4, 6, 7
                ]
            };
        }
    }
}
