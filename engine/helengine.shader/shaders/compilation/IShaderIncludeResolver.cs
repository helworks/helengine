namespace helengine {
    /// <summary>
    /// Resolves include files referenced by shader source code.
    /// </summary>
    public interface IShaderIncludeResolver {
        /// <summary>
        /// Resolves an include path referenced by a shader source file.
        /// </summary>
        /// <param name="requestingFile">Path of the source file that requested the include.</param>
        /// <param name="includePath">Include path as written in the shader source.</param>
        /// <returns>Resolved include contents.</returns>
        ShaderIncludeResult Resolve(string requestingFile, string includePath);
    }
}
