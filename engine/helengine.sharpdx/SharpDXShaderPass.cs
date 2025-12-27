using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.sharpdx {
    /// <summary>
    /// Represents a compiled vertex/pixel shader pair for custom passes.
    /// </summary>
    public sealed class SharpDXShaderPass : IDisposable {
        /// <summary>
        /// Initializes a shader pass by compiling the provided shader entries.
        /// </summary>
        /// <param name="device">Device used to create shader instances.</param>
        /// <param name="shaderPath">Path to the shader source file.</param>
        /// <param name="vertexEntry">Vertex shader entry point.</param>
        /// <param name="pixelEntry">Pixel shader entry point.</param>
        public SharpDXShaderPass(D3DDevice device, string shaderPath, string vertexEntry, string pixelEntry) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }
            if (string.IsNullOrWhiteSpace(shaderPath)) {
                throw new ArgumentException("Shader path must be provided.", nameof(shaderPath));
            }
            if (string.IsNullOrWhiteSpace(vertexEntry)) {
                throw new ArgumentException("Vertex entry point must be provided.", nameof(vertexEntry));
            }
            if (string.IsNullOrWhiteSpace(pixelEntry)) {
                throw new ArgumentException("Pixel entry point must be provided.", nameof(pixelEntry));
            }

            ShaderPath = shaderPath;
            VertexEntry = vertexEntry;
            PixelEntry = pixelEntry;

            using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile(shaderPath, vertexEntry, "vs_4_0")) {
                VertexShader = new VertexShader(device, vertexShaderByteCode);
            }

            using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile(shaderPath, pixelEntry, "ps_4_0")) {
                PixelShader = new PixelShader(device, pixelShaderByteCode);
            }
        }

        /// <summary>
        /// Gets the shader file path for this pass.
        /// </summary>
        public string ShaderPath { get; }

        /// <summary>
        /// Gets the vertex shader entry point.
        /// </summary>
        public string VertexEntry { get; }

        /// <summary>
        /// Gets the pixel shader entry point.
        /// </summary>
        public string PixelEntry { get; }

        /// <summary>
        /// Gets the compiled vertex shader.
        /// </summary>
        public VertexShader VertexShader { get; }

        /// <summary>
        /// Gets the compiled pixel shader.
        /// </summary>
        public PixelShader PixelShader { get; }

        /// <summary>
        /// Releases the shader resources.
        /// </summary>
        public void Dispose() {
            PixelShader.Dispose();
            VertexShader.Dispose();
        }
    }
}
