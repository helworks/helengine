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
        }

        /// <summary>
        /// Gets the shader resource used by this material.
        /// </summary>
        public DirectX11ShaderResource ShaderResource { get; }
    }
}
