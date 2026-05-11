namespace helengine.editor {
    /// <summary>
    /// Creates runtime material instances used by editor-only scene visuals.
    /// </summary>
    public static class EditorVisualMaterialFactory {
        /// <summary>
        /// Creates one standard-material instance that remains visible in the editor but never contributes to shadow maps.
        /// </summary>
        /// <returns>Runtime material instance configured for editor-only visual meshes.</returns>
        public static RuntimeMaterial CreateNonShadowCastingStandardMaterial() {
            RuntimeMaterial sharedStandardMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId);
            RuntimeMaterial resolvedRootMaterial = sharedStandardMaterial.ResolveRootMaterial();
            if (resolvedRootMaterial is not helengine.directx11.DirectX11MaterialResource directX11StandardMaterial) {
                RuntimeMaterial genericMaterialInstance = new RuntimeMaterial();
                if (!string.IsNullOrWhiteSpace(sharedStandardMaterial.Id)) {
                    genericMaterialInstance.SetId(sharedStandardMaterial.Id);
                }
                genericMaterialInstance.SetParentMaterial(sharedStandardMaterial);
                genericMaterialInstance.LightingModel = sharedStandardMaterial.LightingModel;
                genericMaterialInstance.SupportsNormalMapping = sharedStandardMaterial.SupportsNormalMapping;
                genericMaterialInstance.SupportsEmissive = sharedStandardMaterial.SupportsEmissive;
                ApplyEditorOverlayRenderState(genericMaterialInstance);
                return genericMaterialInstance;
            }

            var materialInstance = new helengine.directx11.DirectX11MaterialResource(
                directX11StandardMaterial.ShaderResource,
                directX11StandardMaterial.ShaderAssetId,
                directX11StandardMaterial.VertexProgram,
                directX11StandardMaterial.PixelProgram,
                directX11StandardMaterial.Variant);
            materialInstance.SetId(sharedStandardMaterial.Id);
            materialInstance.SetLayout(sharedStandardMaterial.Layout);
            materialInstance.SetRenderState(sharedStandardMaterial.RenderState);
            materialInstance.Properties.CopyMatchingValuesFrom(sharedStandardMaterial.Properties);
            materialInstance.LightingModel = sharedStandardMaterial.LightingModel;
            materialInstance.SupportsNormalMapping = sharedStandardMaterial.SupportsNormalMapping;
            materialInstance.SupportsEmissive = sharedStandardMaterial.SupportsEmissive;
            ApplyEditorOverlayRenderState(materialInstance);
            return materialInstance;
        }

        /// <summary>
        /// Forces one editor-only visual material to behave like overlay geometry so it remains pickable regardless of scene depth.
        /// </summary>
        /// <param name="material">Editor-only runtime material to configure.</param>
        static void ApplyEditorOverlayRenderState(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            material.CastsShadows = false;
            material.RenderState.BlendMode = MaterialBlendMode.AlphaBlend;
            material.RenderState.DepthTestEnabled = false;
            material.RenderState.DepthWriteEnabled = false;
        }
    }
}
