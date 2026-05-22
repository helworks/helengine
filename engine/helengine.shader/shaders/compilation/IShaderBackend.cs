namespace helengine {
    /// <summary>
    /// Defines a shader compiler backend for a specific API target.
    /// </summary>
    public interface IShaderBackend {
        /// <summary>
        /// Gets the backend target this compiler emits.
        /// </summary>
        ShaderCompileTarget Target { get; }

        /// <summary>
        /// Gets the capabilities supported by the backend.
        /// </summary>
        ShaderBackendCapabilities Capabilities { get; }

        /// <summary>
        /// Compiles the provided shader request into bytecode and reflection metadata.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <param name="includeResolver">Resolver used for shader includes.</param>
        /// <returns>Compilation result.</returns>
        ShaderCompileResult Compile(ShaderCompileRequest request, IShaderIncludeResolver includeResolver);
    }
}
