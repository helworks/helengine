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

            RuntimeModel planeModel = render3D.BuildModelFromRaw(TransformGizmoMeshFactory.CreateCenteredPlaneSquare(CanvasPlaneMeshSize));
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
                Material = planeMaterial
            };
            planeEntity.AddComponent(meshComponent);
            return planeEntity;
        }
    }
}
