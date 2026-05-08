namespace helengine {
    /// <summary>
    /// Represents a serialized material definition that references a shader asset.
    /// </summary>
    public class MaterialAsset : Asset {
        /// <summary>
        /// Initializes a new material asset with default render state and no authored constant-buffer payloads.
        /// </summary>
        public MaterialAsset() {
            RenderState = new MaterialRenderState();
            ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>();
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
        /// Gets or sets the fixed-function render state used while drawing the material.
        /// </summary>
        public MaterialRenderState RenderState;

        /// <summary>
        /// Gets or sets the authored default constant-buffer payloads keyed by shader binding name.
        /// </summary>
        public MaterialConstantBufferAsset[] ConstantBuffers;
    }
}
