namespace helengine {
    /// <summary>
    /// Represents one shader-owned raw material payload used by shader-capable runtime and preview paths.
    /// </summary>
    public class ShaderMaterialAsset : MaterialAsset {
        /// <summary>
        /// Initializes a new shader material asset with default render state and no authored constant-buffer payloads.
        /// </summary>
        public ShaderMaterialAsset() {
            RenderState = new MaterialRenderState();
            ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>();
            CastsShadows = true;
            ReceivesShadows = true;
            ShaderAssetId = string.Empty;
            VertexProgram = string.Empty;
            PixelProgram = string.Empty;
            Variant = string.Empty;
            DiffuseTextureAssetId = string.Empty;
            NormalTextureAssetId = string.Empty;
            EmissiveTextureAssetId = string.Empty;
            RoughnessTextureAssetId = string.Empty;
        }

        /// <summary>
        /// Gets or sets the asset identifier for the shader used by this material.
        /// </summary>
        public string ShaderAssetId;

        /// <summary>
        /// Gets or sets the vertex program name used by this material.
        /// </summary>
        public string VertexProgram;

        /// <summary>
        /// Gets or sets the pixel program name used by this material.
        /// </summary>
        public string PixelProgram;

        /// <summary>
        /// Gets or sets the shader variant name used by this material.
        /// </summary>
        public string Variant;

        /// <summary>
        /// Gets or sets the asset identifier for the authored albedo texture bound by this material.
        /// </summary>
        public string DiffuseTextureAssetId;

        /// <summary>
        /// Gets or sets the asset identifier for the authored normal texture bound by this material.
        /// </summary>
        public string NormalTextureAssetId;

        /// <summary>
        /// Gets or sets the asset identifier for the authored emissive texture bound by this material.
        /// </summary>
        public string EmissiveTextureAssetId;

        /// <summary>
        /// Gets or sets the asset identifier for the authored roughness texture bound by this material.
        /// </summary>
        public string RoughnessTextureAssetId;

        /// <summary>
        /// Gets or sets the fixed-function render state used while drawing the material.
        /// </summary>
        public new MaterialRenderState RenderState;

        /// <summary>
        /// Gets or sets the authored default constant-buffer payloads keyed by shader binding name.
        /// </summary>
        public MaterialConstantBufferAsset[] ConstantBuffers;

        /// <summary>
        /// Gets or sets whether the material contributes geometry to shadow-map passes.
        /// </summary>
        public new bool CastsShadows;

        /// <summary>
        /// Gets or sets whether the material receives shadow attenuation during lighting.
        /// </summary>
        public new bool ReceivesShadows;
    }
}
