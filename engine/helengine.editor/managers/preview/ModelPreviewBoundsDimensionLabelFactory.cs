namespace helengine.editor {
    /// <summary>
    /// Creates world-space dimension labels for the positive-facing edges of a model preview bounding box.
    /// </summary>
    public static class ModelPreviewBoundsDimensionLabelFactory {
        /// <summary>
        /// Render order that keeps dimension text above the preview bounds line overlays.
        /// </summary>
        const byte DimensionLabelRenderOrder3D = 3;

        /// <summary>
        /// Creates width, height, and depth label entities centered on positive-facing bounding-box edges.
        /// </summary>
        /// <param name="render3D">Renderer used to build glyph runtime models and their shared material.</param>
        /// <param name="font">Shared editor font used by transform-gizmo axis labels.</param>
        /// <param name="halfExtents">Half-size of the centered preview bounds on each model axis.</param>
        /// <returns>Three entities ordered by X width, Y height, and Z depth.</returns>
        public static EditorEntity[] Create(RenderManager3D render3D, FontAsset font, float3 halfExtents, EditorBuiltInShaderAssetLibrary builtInShaderLibrary) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (halfExtents.X < 0f || halfExtents.Y < 0f || halfExtents.Z < 0f) {
                throw new ArgumentOutOfRangeException(nameof(halfExtents), "Bounding-box half extents cannot be negative.");
            }
            if (builtInShaderLibrary == null) {
                throw new ArgumentNullException(nameof(builtInShaderLibrary));
            }

            RuntimeMaterial labelMaterial = TransformGizmoAxisLabelMaterialFactory.Create(render3D, font, builtInShaderLibrary);
            float3 dimensions = halfExtents * 2f;
            return new[] {
                CreateDimensionLabelEntity(render3D, font, labelMaterial, "X", dimensions.X, new float3(0f, halfExtents.Y, halfExtents.Z)),
                CreateDimensionLabelEntity(render3D, font, labelMaterial, "Y", dimensions.Y, new float3(halfExtents.X, 0f, halfExtents.Z)),
                CreateDimensionLabelEntity(render3D, font, labelMaterial, "Z", dimensions.Z, new float3(halfExtents.X, halfExtents.Y, 0f))
            };
        }

        /// <summary>
        /// Creates one hidden preview-layer glyph billboard for a formatted bounds dimension.
        /// </summary>
        /// <param name="render3D">Renderer used to upload the glyph model.</param>
        /// <param name="font">Font atlas used to generate the glyph mesh.</param>
        /// <param name="labelMaterial">Shared transform-gizmo label material.</param>
        /// <param name="axisName">Axis name included in the diagnostic entity name.</param>
        /// <param name="dimension">Full bounds length displayed by the label.</param>
        /// <param name="position">Centered positive-facing bounds-edge position.</param>
        /// <returns>Configured but disabled dimension label entity.</returns>
        static EditorEntity CreateDimensionLabelEntity(
            RenderManager3D render3D,
            FontAsset font,
            RuntimeMaterial labelMaterial,
            string axisName,
            float dimension,
            float3 position) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (labelMaterial == null) {
                throw new ArgumentNullException(nameof(labelMaterial));
            }
            if (string.IsNullOrWhiteSpace(axisName)) {
                throw new ArgumentException("Axis name must be provided.", nameof(axisName));
            }

            string text = dimension.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            ModelAsset modelAsset = TransformGizmoAxisLabelModelFactory.Create(font, text);
            RuntimeModel model = render3D.BuildModelFromRaw(modelAsset);
            Core ownerCore = render3D.OwnerCore ?? throw new InvalidOperationException("Preview dimension-label renderer must be bound to an owning core.");
            var entity = new EditorEntity(ownerCore) {
                Name = "Model Preview Bounds " + axisName + " Dimension",
                Hidden = true,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneModelPreview,
                LocalPosition = position,
                Enabled = false
            };
            var mesh = new MeshComponent {
                Model = model,
                Materials = new[] { labelMaterial },
                RenderOrder3D = DimensionLabelRenderOrder3D
            };
            entity.AddComponent(mesh);
            return entity;
        }
    }
}
