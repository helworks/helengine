using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.directx11 {
    /// <summary>
    /// DirectX11 shader resource containing a vertex/pixel shader pair and input layout.
    /// </summary>
    public class DirectX11ShaderResource : IDisposable {
        /// <summary>
        /// Initializes a new DirectX11 shader resource from compiled bytecode.
        /// </summary>
        /// <param name="device">Device used to create shader instances.</param>
        /// <param name="vertexBytecode">Compiled vertex shader bytecode.</param>
        /// <param name="pixelBytecode">Compiled pixel shader bytecode.</param>
        /// <param name="inputElements">Input layout elements matching the vertex shader.</param>
        /// <param name="vertexProgramName">Logical vertex program name.</param>
        /// <param name="pixelProgramName">Logical pixel program name.</param>
        /// <param name="variant">Shader variant name.</param>
        public DirectX11ShaderResource(
            D3DDevice device,
            byte[] vertexBytecode,
            byte[] pixelBytecode,
            InputElement[] inputElements,
            string vertexProgramName,
            string pixelProgramName,
            string variant) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            if (vertexBytecode == null || vertexBytecode.Length == 0) {
                throw new ArgumentException("Vertex bytecode must be provided.", nameof(vertexBytecode));
            }

            if (pixelBytecode == null || pixelBytecode.Length == 0) {
                throw new ArgumentException("Pixel bytecode must be provided.", nameof(pixelBytecode));
            }

            if (inputElements == null || inputElements.Length == 0) {
                throw new ArgumentException("Input elements must be provided.", nameof(inputElements));
            }

            if (string.IsNullOrWhiteSpace(vertexProgramName)) {
                throw new ArgumentException("Vertex program name must be provided.", nameof(vertexProgramName));
            }

            if (string.IsNullOrWhiteSpace(pixelProgramName)) {
                throw new ArgumentException("Pixel program name must be provided.", nameof(pixelProgramName));
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new ArgumentException("Variant must be provided.", nameof(variant));
            }

            VertexProgramName = vertexProgramName;
            PixelProgramName = pixelProgramName;
            Variant = variant;

            using (var vertexShaderBytecode = new ShaderBytecode(vertexBytecode)) {
                VertexShader = new VertexShader(device, vertexShaderBytecode);
                var signature = ShaderSignature.GetInputSignature(vertexShaderBytecode);
                InputLayout = new InputLayout(device, signature, inputElements);
            }

            PixelShader = new PixelShader(device, pixelBytecode);
        }

        /// <summary>
        /// Gets the logical vertex program name used to build this shader.
        /// </summary>
        public string VertexProgramName { get; }

        /// <summary>
        /// Gets the logical pixel program name used to build this shader.
        /// </summary>
        public string PixelProgramName { get; }

        /// <summary>
        /// Gets the shader variant name used to build this shader.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets the compiled vertex shader instance.
        /// </summary>
        public VertexShader VertexShader { get; }

        /// <summary>
        /// Gets the compiled pixel shader instance.
        /// </summary>
        public PixelShader PixelShader { get; }

        /// <summary>
        /// Gets the input layout associated with the vertex shader.
        /// </summary>
        public InputLayout InputLayout { get; }

        /// <summary>
        /// Releases the shader resources.
        /// </summary>
        public void Dispose() {
            InputLayout.Dispose();
            PixelShader.Dispose();
            VertexShader.Dispose();
        }
    }
}
