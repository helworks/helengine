namespace helengine.editor {
    /// <summary>
    /// Builds the textured unlit material used by editor-only exact 2D world preview proxies.
    /// </summary>
    public static class EditorExact2DPreviewMaterialFactory {
        /// <summary>
        /// Built-in shader source file used by exact 2D preview materials.
        /// </summary>
        const string ShaderFileName = "EditorExact2DPreview.hlsl";

        /// <summary>
        /// Material asset identifier used by exact 2D preview materials.
        /// </summary>
        const string MaterialAssetId = "EditorExact2DPreview.material";

        /// <summary>
        /// Vertex program name used by the runtime exact 2D preview shader.
        /// </summary>
        const string VertexProgramName = "EditorExact2DPreview.vs";

        /// <summary>
        /// Pixel program name used by the runtime exact 2D preview shader.
        /// </summary>
        const string PixelProgramName = "EditorExact2DPreview.ps";

        /// <summary>
        /// Variant name used by the runtime exact 2D preview shader.
        /// </summary>
        const string VariantName = "default";

        /// <summary>
        /// Builds the runtime material used by one exact world-space 2D preview proxy.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <param name="texture">Runtime texture displayed by the preview quad.</param>
        /// <returns>Runtime material configured to sample the supplied preview texture.</returns>
        public static RuntimeMaterial Create(RenderManager3D render3D, RuntimeTexture texture) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            } else if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(render3D, ShaderFileName);
            MaterialAsset materialAsset = new MaterialAsset {
                Id = MaterialAssetId,
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = VertexProgramName,
                PixelProgram = PixelProgramName,
                Variant = VariantName,
                RenderState = new MaterialRenderState {
                    BlendMode = MaterialBlendMode.AlphaBlend,
                    CullMode = MaterialCullMode.None,
                    DepthTestEnabled = true,
                    DepthWriteEnabled = false
                }
            };

            RuntimeMaterial material = render3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
            material.LightingModel = RuntimeMaterialLightingModel.Unlit;
            material.Properties.SetTexture("PreviewTexture", texture);
            return material;
        }
    }
}
