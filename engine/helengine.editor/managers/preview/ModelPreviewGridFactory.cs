namespace helengine.editor {
    /// <summary>
    /// Builds the internal floor grid rendered beneath one offscreen model preview.
    /// </summary>
    public static class ModelPreviewGridFactory {
        /// <summary>
        /// Display name assigned to preview-grid entities for diagnostics.
        /// </summary>
        const string GridEntityName = "Model Preview Grid";
        /// <summary>
        /// Render order that draws the transparent grid after the preview model while retaining scene depth testing.
        /// </summary>
        const byte GridRenderOrder3D = 1;

        /// <summary>
        /// Creates one XZ-aligned grid sized for a single model preview.
        /// </summary>
        /// <param name="render3D">Renderer used to create the grid's runtime resources.</param>
        /// <param name="sideLength">Side length of the square grid in model world units.</param>
        /// <returns>Configured internal grid entity.</returns>
        public static EditorEntity Create(RenderManager3D render3D, float sideLength) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (sideLength <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(sideLength), "Preview grid side length must be greater than zero.");
            }

            RuntimeModel gridModel = render3D.BuildModelFromRaw(TransformGizmoMeshFactory.CreateCenteredPlaneSquare(sideLength));
            RuntimeMaterial gridMaterial = EditorViewportGridMaterialFactory.Create(render3D);
            var gridEntity = new EditorEntity {
                Name = GridEntityName,
                Hidden = true,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneModelPreview,
                LocalOrientation = CreateXzPlaneOrientation()
            };
            var gridMesh = new MeshComponent {
                Model = gridModel,
                Materials = new[] { gridMaterial },
                RenderOrder3D = GridRenderOrder3D
            };
            gridEntity.AddComponent(gridMesh);
            return gridEntity;
        }

        /// <summary>
        /// Creates the plane rotation that maps the local XY mesh plane onto the world XZ plane.
        /// </summary>
        /// <returns>Quaternion rotating local positive Y toward world positive Z.</returns>
        static float4 CreateXzPlaneOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5d), out orientation);
            return orientation;
        }
    }
}
