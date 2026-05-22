namespace helengine.editor {
    /// <summary>
    /// Builds the unlit material used by editor-only authored viewport border gizmos.
    /// </summary>
    public static class EditorViewportBorderGizmoMaterialFactory {
        /// <summary>
        /// Built-in shader source file used by authored viewport border gizmos.
        /// </summary>
        const string ShaderFileName = "EditorViewportBorderGizmo.hlsl";

        /// <summary>
        /// Material asset identifier used by authored viewport border gizmos.
        /// </summary>
        const string MaterialAssetId = "EditorViewportBorderGizmo.material";

        /// <summary>
        /// Vertex program name used by the runtime viewport-border shader.
        /// </summary>
        const string VertexProgramName = "EditorViewportBorderGizmo.vs";

        /// <summary>
        /// Pixel program name used by the runtime viewport-border shader.
        /// </summary>
        const string PixelProgramName = "EditorViewportBorderGizmo.ps";

        /// <summary>
        /// Variant name used by the runtime viewport-border shader.
        /// </summary>
        const string VariantName = "default";

        /// <summary>
        /// Builds the runtime material used by one authored viewport border gizmo.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Configured runtime material for one viewport border gizmo.</returns>
        public static RuntimeMaterial Create(RenderManager3D render3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
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
            return material;
        }
    }
}
