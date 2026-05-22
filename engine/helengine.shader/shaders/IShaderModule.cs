namespace helengine {
    /// <summary>
    /// Provides access to generated shader metadata for a single shader module.
    /// </summary>
    public interface IShaderModule {
        /// <summary>
        /// Builds the shader module definition using the provided module root.
        /// </summary>
        /// <param name="moduleRoot">Root directory containing the compiled shader binaries.</param>
        /// <returns>Shader module definition with resolved binary paths.</returns>
        ShaderModuleDefinition BuildDefinition(string moduleRoot);
    }
}
