namespace helengine.editor {
    /// <summary>
    /// Builds the textured unlit material used by editor-only world-space sprite preview proxies.
    /// </summary>
    public static class EditorWorldSpaceSpritePreviewMaterialFactory {
        /// <summary>
        /// Built-in shader source file used by sprite world-preview materials.
        /// </summary>
        const string ShaderFileName = "EditorWorldSpaceSpritePreview.hlsl";
        /// <summary>
        /// Material asset identifier used by sprite world-preview materials.
        /// </summary>
        const string MaterialAssetId = "EditorWorldSpaceSpritePreview.material";
        /// <summary>
        /// Vertex program name used by the runtime sprite world-preview shader.
        /// </summary>
        const string VertexProgramName = "EditorWorldSpaceSpritePreview.vs";
        /// <summary>
        /// Pixel program name used by the runtime sprite world-preview shader.
        /// </summary>
        const string PixelProgramName = "EditorWorldSpaceSpritePreview.ps";
        /// <summary>
        /// Variant name used by the runtime sprite world-preview shader.
        /// </summary>
        const string VariantName = "default";

        /// <summary>
        /// Builds the runtime material used by one world-space sprite preview proxy.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <param name="texture">Runtime texture displayed by the preview sprite.</param>
        /// <returns>Runtime material configured to sample the supplied sprite texture.</returns>
        public static RuntimeMaterial Create(RenderManager3D render3D, RuntimeTexture texture) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            } else if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(render3D, ShaderFileName);
            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
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
            ShaderRuntimeMaterialAccess.Require(material).Properties.SetTexture("PreviewTexture", texture);
            return material;
        }
    }
}
