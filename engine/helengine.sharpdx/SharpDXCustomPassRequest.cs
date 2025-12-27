namespace helengine.sharpdx {
    /// <summary>
    /// Describes a one-frame custom shader pass request for the SharpDX renderer.
    /// </summary>
    public sealed class SharpDXCustomPassRequest {
        /// <summary>
        /// Initializes a new custom pass request with the required shader and color provider.
        /// </summary>
        /// <param name="camera">Camera providing transform, viewport, and target information.</param>
        /// <param name="renderQueue">Render queue supplying drawables for the pass.</param>
        /// <param name="shaderPath">Path to the shader file.</param>
        /// <param name="vertexEntry">Entry point for the vertex shader.</param>
        /// <param name="pixelEntry">Entry point for the pixel shader.</param>
        /// <param name="colorProvider">Function supplying per-draw colors for the shader.</param>
        public SharpDXCustomPassRequest(
            ICamera camera,
            IRenderQueue3D renderQueue,
            string shaderPath,
            string vertexEntry,
            string pixelEntry,
            Func<IDrawable3D, byte4> colorProvider) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            if (renderQueue == null) {
                throw new ArgumentNullException(nameof(renderQueue));
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
            if (colorProvider == null) {
                throw new ArgumentNullException(nameof(colorProvider));
            }

            Camera = camera;
            RenderQueue = renderQueue;
            ShaderPath = shaderPath;
            VertexEntry = vertexEntry;
            PixelEntry = pixelEntry;
            ColorProvider = colorProvider;
        }

        /// <summary>
        /// Gets the camera providing transform, viewport, and target information.
        /// </summary>
        public ICamera Camera { get; }

        /// <summary>
        /// Gets the render queue supplying drawables for the pass.
        /// </summary>
        public IRenderQueue3D RenderQueue { get; }

        /// <summary>
        /// Gets the shader file path for the pass.
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
        /// Gets the function that supplies per-draw colors for the shader.
        /// </summary>
        public Func<IDrawable3D, byte4> ColorProvider { get; }
    }
}
