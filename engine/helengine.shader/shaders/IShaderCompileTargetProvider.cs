namespace helengine {
    /// <summary>
    /// Exposes the shader compile target associated with a renderer abstraction that is not tied to a concrete backend type.
    /// </summary>
    public interface IShaderCompileTargetProvider {
        /// <summary>
        /// Gets the shader compile target used by the runtime renderer.
        /// </summary>
        ShaderCompileTarget ShaderCompileTarget { get; }
    }
}
