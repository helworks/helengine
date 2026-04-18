namespace helengine.editor {
    /// <summary>
    /// Creates the internal world grid rendered in empty editor scenes.
    /// </summary>
    public static class EditorViewportGridFactory {
        /// <summary>
        /// Display name assigned to the internal viewport-grid entity.
        /// </summary>
        const string GridEntityName = "Viewport Grid";
        /// <summary>
        /// Side length of the viewport grid in world units.
        /// </summary>
        const float GridSize = 10f;
        /// <summary>
        /// Small vertical offset applied below world zero to avoid z-fighting with authored planes.
        /// </summary>
        const float GridVerticalOffset = -0.001f;
        /// <summary>
        /// Render order used by the viewport-grid mesh so it renders after default opaque scene geometry.
        /// </summary>
        const byte GridRenderOrder3D = 1;

        /// <summary>
        /// Creates the internal world grid rendered by the scene camera.
        /// </summary>
        /// <param name="render3D">Renderer used to build runtime mesh and material resources.</param>
        /// <returns>Configured internal viewport-grid entity.</returns>
        public static EditorEntity Create(RenderManager3D render3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            RuntimeModel gridModel = render3D.BuildModelFromRaw(TransformGizmoMeshFactory.CreateCenteredPlaneSquare(GridSize));
            RuntimeMaterial gridMaterial = EditorViewportGridMaterialFactory.Create(render3D);
            float4 gridOrientation = CreateXzPlaneOrientation();
            var gridEntity = new EditorEntity {
                Name = GridEntityName,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneObjects,
                LocalPosition = new float3(0f, GridVerticalOffset, 0f),
                LocalScale = float3.One,
                LocalOrientation = gridOrientation
            };
            var meshComponent = new MeshComponent {
                Model = gridModel,
                Material = gridMaterial,
                RenderOrder3D = GridRenderOrder3D
            };
            gridEntity.AddComponent(meshComponent);
            return gridEntity;
        }

        /// <summary>
        /// Creates the plane rotation that maps the local XY plane to the world XZ plane.
        /// </summary>
        /// <returns>Quaternion rotating local +Y into world +Z.</returns>
        static float4 CreateXzPlaneOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }
    }
}
