namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides one minimal shader backend used by editor tests that only need target registration behavior.
    /// </summary>
    public sealed class TestShaderBackend : IShaderBackend {
        /// <summary>
        /// Initializes one test shader backend for the supplied compile target.
        /// </summary>
        /// <param name="target">Compile target reported by the backend.</param>
        public TestShaderBackend(ShaderCompileTarget target) {
            Target = target;
            Capabilities = new ShaderBackendCapabilities(
                new ShaderModel(4, 0),
                new ShaderModel(4, 0),
                [ShaderStage.Vertex, ShaderStage.Pixel],
                false);
        }

        /// <summary>
        /// Gets the compile target reported by the backend.
        /// </summary>
        public ShaderCompileTarget Target { get; }

        /// <summary>
        /// Gets the static capability description exposed by the backend.
        /// </summary>
        public ShaderBackendCapabilities Capabilities { get; }

        /// <summary>
        /// Throws because editor catalog tests never execute real shader compilation through the test backend.
        /// </summary>
        /// <param name="request">Compilation request.</param>
        /// <param name="includeResolver">Shader include resolver.</param>
        /// <returns>Never returns because the test backend does not support compilation.</returns>
        public ShaderCompileResult Compile(ShaderCompileRequest request, IShaderIncludeResolver includeResolver) {
            throw new NotSupportedException("The test shader backend does not compile shaders.");
        }
    }
}
