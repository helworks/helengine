namespace helengine.editor {
    /// <summary>
    /// Creates runtime material instances used by editor-only scene visuals.
    /// </summary>
    public static class EditorVisualMaterialFactory {
        /// <summary>
        /// Creates one standard-material instance that remains visible in the editor but still participates in normal scene depth.
        /// </summary>
        /// <param name="generatedMaterialCache">Cache that owns the generated standard material and its host clone policy.</param>
        /// <returns>Runtime material instance configured for editor-only visual meshes.</returns>
        public static RuntimeMaterial CreateNonShadowCastingStandardMaterial(EngineGeneratedMaterialCache generatedMaterialCache) {
            if (generatedMaterialCache == null) {
                throw new ArgumentNullException(nameof(generatedMaterialCache));
            }

            RuntimeMaterial material = generatedMaterialCache.CreateRuntimeMaterialInstance(
                generatedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId));
            ApplyEditorVisualRenderState(material);
            return material;
        }

        /// <summary>
        /// Creates one standard-material instance that behaves like overlay geometry for editor icons that must remain visible on top.
        /// </summary>
        /// <param name="generatedMaterialCache">Cache that owns the generated standard material and its host clone policy.</param>
        /// <returns>Runtime material configured for editor-only overlay visuals.</returns>
        public static RuntimeMaterial CreateOverlayStandardMaterial(EngineGeneratedMaterialCache generatedMaterialCache) {
            RuntimeMaterial material = CreateNonShadowCastingStandardMaterial(generatedMaterialCache);
            ApplyEditorOverlayRenderState(material);
            return material;
        }

        /// <summary>
        /// Applies the shared non-shadow-casting editor visual material state without changing normal scene depth behavior.
        /// </summary>
        /// <param name="material">Editor-only runtime material to configure.</param>
        static void ApplyEditorVisualRenderState(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            material.CastsShadows = false;
        }

        /// <summary>
        /// Forces one editor-only visual material to behave like overlay geometry so it remains visible regardless of scene depth.
        /// </summary>
        /// <param name="material">Editor-only runtime material to configure.</param>
        static void ApplyEditorOverlayRenderState(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            ApplyEditorVisualRenderState(material);
            material.RenderState.BlendMode = MaterialBlendMode.AlphaBlend;
            material.RenderState.DepthTestEnabled = false;
            material.RenderState.DepthWriteEnabled = false;
        }
    }
}
