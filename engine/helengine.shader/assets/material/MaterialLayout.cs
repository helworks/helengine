namespace helengine {
    /// <summary>
    /// Describes the shader bindings and render state that a shader runtime material exposes to shader-capable backends.
    /// </summary>
    public class MaterialLayout : IDisposable {
        /// <summary>
        /// Shared empty layout used by shader runtime materials that have not yet been configured from shader metadata.
        /// </summary>
        static readonly MaterialLayout EmptyValue = CreateEmptyValue();

        /// <summary>
        /// Shader asset identifier that owns the selected programs.
        /// </summary>
        string ShaderAssetIdValue;

        /// <summary>
        /// Vertex program selected by the material.
        /// </summary>
        string VertexProgramValue;

        /// <summary>
        /// Pixel program selected by the material.
        /// </summary>
        string PixelProgramValue;

        /// <summary>
        /// Shader variant selected by the material.
        /// </summary>
        string VariantValue;

        /// <summary>
        /// Render state associated with this layout.
        /// </summary>
        MaterialRenderState RenderStateValue;

        /// <summary>
        /// Texture bindings exposed by the selected shader programs.
        /// </summary>
        MaterialLayoutBinding[] TextureBindingsValue;

        /// <summary>
        /// Constant-buffer bindings exposed by the selected shader programs.
        /// </summary>
        MaterialLayoutBinding[] ConstantBufferBindingsValue;

        /// <summary>
        /// Sampler bindings exposed by the selected shader programs.
        /// </summary>
        MaterialLayoutBinding[] SamplerBindingsValue;

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
            ShaderAssetIdValue = shaderAssetId ?? throw new ArgumentNullException(nameof(shaderAssetId));
            VertexProgramValue = vertexProgram ?? throw new ArgumentNullException(nameof(vertexProgram));
            PixelProgramValue = pixelProgram ?? throw new ArgumentNullException(nameof(pixelProgram));
            VariantValue = variant ?? throw new ArgumentNullException(nameof(variant));
            RenderStateValue = renderState ?? throw new ArgumentNullException(nameof(renderState));
            TextureBindingsValue = textureBindings ?? throw new ArgumentNullException(nameof(textureBindings));
            ConstantBufferBindingsValue = constantBufferBindings ?? throw new ArgumentNullException(nameof(constantBufferBindings));
            SamplerBindingsValue = samplerBindings ?? throw new ArgumentNullException(nameof(samplerBindings));
        }

        /// <summary>
        /// Gets the empty shared layout used before a shader runtime material is configured.
        /// </summary>
        public static MaterialLayout Empty => EmptyValue;

        /// <summary>
        /// Builds the shared empty layout used before a shader runtime material is configured.
        /// </summary>
        /// <returns>Shared empty material layout.</returns>
        static MaterialLayout CreateEmptyValue() {
            return new MaterialLayout(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                new MaterialRenderState(),
                Array.Empty<MaterialLayoutBinding>(),
                Array.Empty<MaterialLayoutBinding>(),
                Array.Empty<MaterialLayoutBinding>());
        }

        /// <summary>
        /// Gets the shader asset identifier that owns the selected programs.
        /// </summary>
        public string ShaderAssetId => ShaderAssetIdValue;

        /// <summary>
        /// Gets the vertex program selected by the material.
        /// </summary>
        public string VertexProgram => VertexProgramValue;

        /// <summary>
        /// Gets the pixel program selected by the material.
        /// </summary>
        public string PixelProgram => PixelProgramValue;

        /// <summary>
        /// Gets the shader variant selected by the material.
        /// </summary>
        public string Variant => VariantValue;

        /// <summary>
        /// Gets the render state associated with the material layout.
        /// </summary>
        public MaterialRenderState RenderState => RenderStateValue;

        /// <summary>
        /// Gets the texture bindings exposed by the selected shader programs.
        /// </summary>
        public MaterialLayoutBinding[] TextureBindings => TextureBindingsValue;

        /// <summary>
        /// Gets the constant-buffer bindings exposed by the selected shader programs.
        /// </summary>
        public MaterialLayoutBinding[] ConstantBufferBindings => ConstantBufferBindingsValue;

        /// <summary>
        /// Gets the sampler bindings exposed by the selected shader programs.
        /// </summary>
        public MaterialLayoutBinding[] SamplerBindings => SamplerBindingsValue;

        /// <summary>
        /// Releases layout-owned native containers for bindings and render state.
        /// </summary>
        public void Dispose() {
            if (ReferenceEquals(this, EmptyValue)) {
                return;
            }

            NativeOwnership.Delete(RenderStateValue);
            RenderStateValue = null;
            ReleaseBindings(ref TextureBindingsValue);
            ReleaseBindings(ref ConstantBufferBindingsValue);
            ReleaseBindings(ref SamplerBindingsValue);
            ShaderAssetIdValue = null;
            VertexProgramValue = null;
            PixelProgramValue = null;
            VariantValue = null;
        }

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

        /// <summary>
        /// Releases one binding array unless it points at the shared framework empty-array singleton.
        /// </summary>
        /// <param name="bindings">Binding array to release.</param>
        static void ReleaseBindings(ref MaterialLayoutBinding[] bindings) {
            if (!ReferenceEquals(bindings, Array.Empty<MaterialLayoutBinding>())) {
                NativeOwnership.DeleteItemsAndRelease(ref bindings);
            }

            bindings = null;
        }
    }
}
