namespace helengine {
    /// <summary>
    /// Describes the shader bindings and render state that a runtime material exposes to the engine.
    /// </summary>
    public class MaterialLayout {
        /// <summary>
        /// Shared empty layout used by runtime materials that have not yet been configured from shader metadata.
        /// </summary>
        static readonly MaterialLayout EmptyValue = new MaterialLayout(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            new MaterialRenderState(),
            Array.Empty<MaterialLayoutBinding>(),
            Array.Empty<MaterialLayoutBinding>(),
            Array.Empty<MaterialLayoutBinding>());

        /// <summary>
        /// Initializes a new material layout.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier that owns the selected programs.</param>
        /// <param name="vertexProgram">Vertex program selected by the material.</param>
        /// <param name="pixelProgram">Pixel program selected by the material.</param>
        /// <param name="variant">Shader variant selected by the material.</param>
        /// <param name="renderState">Render state associated with the material layout.</param>
        /// <param name="textureBindings">Texture bindings exposed by the selected shader programs.</param>
        /// <param name="constantBufferBindings">Constant-buffer bindings exposed by the selected shader programs.</param>
        /// <param name="samplerBindings">Sampler bindings exposed by the selected shader programs.</param>
        public MaterialLayout(
            string shaderAssetId,
            string vertexProgram,
            string pixelProgram,
            string variant,
            MaterialRenderState renderState,
            MaterialLayoutBinding[] textureBindings,
            MaterialLayoutBinding[] constantBufferBindings,
            MaterialLayoutBinding[] samplerBindings) {
            ShaderAssetId = shaderAssetId ?? throw new ArgumentNullException(nameof(shaderAssetId));
            VertexProgram = vertexProgram ?? throw new ArgumentNullException(nameof(vertexProgram));
            PixelProgram = pixelProgram ?? throw new ArgumentNullException(nameof(pixelProgram));
            Variant = variant ?? throw new ArgumentNullException(nameof(variant));
            RenderState = renderState ?? throw new ArgumentNullException(nameof(renderState));
            TextureBindings = textureBindings ?? throw new ArgumentNullException(nameof(textureBindings));
            ConstantBufferBindings = constantBufferBindings ?? throw new ArgumentNullException(nameof(constantBufferBindings));
            SamplerBindings = samplerBindings ?? throw new ArgumentNullException(nameof(samplerBindings));
        }

        /// <summary>
        /// Gets the empty shared layout used before a runtime material is configured.
        /// </summary>
        public static MaterialLayout Empty => EmptyValue;

        /// <summary>
        /// Gets the shader asset identifier that owns the selected programs.
        /// </summary>
        public string ShaderAssetId { get; }

        /// <summary>
        /// Gets the vertex program selected by the material.
        /// </summary>
        public string VertexProgram { get; }

        /// <summary>
        /// Gets the pixel program selected by the material.
        /// </summary>
        public string PixelProgram { get; }

        /// <summary>
        /// Gets the shader variant selected by the material.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets the render state associated with the material layout.
        /// </summary>
        public MaterialRenderState RenderState { get; }

        /// <summary>
        /// Gets the texture bindings exposed by the selected shader programs.
        /// </summary>
        public MaterialLayoutBinding[] TextureBindings { get; }

        /// <summary>
        /// Gets the constant-buffer bindings exposed by the selected shader programs.
        /// </summary>
        public MaterialLayoutBinding[] ConstantBufferBindings { get; }

        /// <summary>
        /// Gets the sampler bindings exposed by the selected shader programs.
        /// </summary>
        public MaterialLayoutBinding[] SamplerBindings { get; }

        /// <summary>
        /// Locates the texture-binding index for the supplied binding name.
        /// </summary>
        /// <param name="bindingName">Binding name to resolve.</param>
        /// <returns>Texture-binding index when found; otherwise <c>-1</c>.</returns>
        public int FindTextureBindingIndex(string bindingName) {
            return FindBindingIndex(TextureBindings, bindingName);
        }

        /// <summary>
        /// Locates the constant-buffer binding index for the supplied binding name.
        /// </summary>
        /// <param name="bindingName">Binding name to resolve.</param>
        /// <returns>Constant-buffer binding index when found; otherwise <c>-1</c>.</returns>
        public int FindConstantBufferBindingIndex(string bindingName) {
            return FindBindingIndex(ConstantBufferBindings, bindingName);
        }

        /// <summary>
        /// Locates the sampler-binding index for the supplied binding name.
        /// </summary>
        /// <param name="bindingName">Binding name to resolve.</param>
        /// <returns>Sampler-binding index when found; otherwise <c>-1</c>.</returns>
        public int FindSamplerBindingIndex(string bindingName) {
            return FindBindingIndex(SamplerBindings, bindingName);
        }

        /// <summary>
        /// Finds the index of a binding with the supplied name inside one binding array.
        /// </summary>
        /// <param name="bindings">Binding array to inspect.</param>
        /// <param name="bindingName">Binding name to resolve.</param>
        /// <returns>Binding index when found; otherwise <c>-1</c>.</returns>
        static int FindBindingIndex(MaterialLayoutBinding[] bindings, string bindingName) {
            if (bindings == null) {
                throw new ArgumentNullException(nameof(bindings));
            }

            if (string.IsNullOrWhiteSpace(bindingName)) {
                throw new ArgumentException("Binding name must be provided.", nameof(bindingName));
            }

            for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++) {
                MaterialLayoutBinding binding = bindings[bindingIndex];
                if (binding == null) {
                    continue;
                }

                if (string.Equals(binding.Name, bindingName, StringComparison.Ordinal)) {
                    return bindingIndex;
                }
            }

            return -1;
        }
    }
}
