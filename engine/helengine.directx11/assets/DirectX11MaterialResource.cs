namespace helengine.directx11 {
    /// <summary>
    /// DirectX11-backed runtime material resource.
    /// </summary>
    public class DirectX11MaterialResource : RuntimeMaterial {
        /// <summary>
        /// Initializes a new DirectX11 material with the specified shader resource.
        /// </summary>
        /// <param name="shaderResource">Shader resource used by the material.</param>
        public DirectX11MaterialResource(DirectX11ShaderResource shaderResource) {
            if (shaderResource == null) {
                throw new ArgumentNullException(nameof(shaderResource));
            }

            ShaderResource = shaderResource;
            ShaderAssetId = string.Empty;
            VertexProgram = string.Empty;
            PixelProgram = string.Empty;
            Variant = string.Empty;
            CastsShadows = true;
        }

        /// <summary>
        /// Initializes a new DirectX11 material with shader metadata.
        /// </summary>
        /// <param name="shaderResource">Shader resource used by the material.</param>
        /// <param name="shaderAssetId">Shader asset identifier.</param>
        /// <param name="vertexProgram">Vertex program name.</param>
        /// <param name="pixelProgram">Pixel program name.</param>
        /// <param name="variant">Shader variant name.</param>
        public DirectX11MaterialResource(
            DirectX11ShaderResource shaderResource,
            string shaderAssetId,
            string vertexProgram,
            string pixelProgram,
            string variant) {
            if (shaderResource == null) {
                throw new ArgumentNullException(nameof(shaderResource));
            }

            ShaderResource = shaderResource;
            ShaderAssetId = shaderAssetId ?? string.Empty;
            VertexProgram = vertexProgram ?? string.Empty;
            PixelProgram = pixelProgram ?? string.Empty;
            Variant = variant ?? string.Empty;
            CastsShadows = true;
        }

        /// <summary>
        /// Gets the shader resource used by this material.
        /// </summary>
        public DirectX11ShaderResource ShaderResource { get; private set; }

        /// <summary>
        /// Gets the shader asset identifier for this material.
        /// </summary>
        public string ShaderAssetId { get; }

        /// <summary>
        /// Gets the vertex program name used by this material.
        /// </summary>
        public string VertexProgram { get; }

        /// <summary>
        /// Gets the pixel program name used by this material.
        /// </summary>
        public string PixelProgram { get; }

        /// <summary>
        /// Gets the shader variant name used by this material.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets or sets whether this DirectX11 material root should contribute geometry to shadow-map passes.
        /// Child runtime materials inherit this eligibility by resolving back to their DirectX11 root material.
        /// </summary>
        public bool CastsShadows { get; set; }

        /// <summary>
        /// Gets whether this resource uses the compact Windows-forward PBR material path.
        /// </summary>
        public bool UsesCompactPbrLightingModel => LightingModel == RuntimeMaterialLightingModel.MetalRoughPbr;

        /// <summary>
        /// Updates the shader resource used by this material.
        /// </summary>
        /// <param name="shaderResource">New shader resource to assign.</param>
        public void UpdateShaderResource(DirectX11ShaderResource shaderResource) {
            if (shaderResource == null) {
                throw new ArgumentNullException(nameof(shaderResource));
            }

            ShaderResource = shaderResource;
        }
    }
}
