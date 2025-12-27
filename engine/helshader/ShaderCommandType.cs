namespace helshader {
    /// <summary>
    /// Identifies the command selected for the shader tool.
    /// </summary>
    public enum ShaderCommandType {
        /// <summary>
        /// No command was specified.
        /// </summary>
        None,

        /// <summary>
        /// Build shaders and generate code.
        /// </summary>
        Build,

        /// <summary>
        /// Generate code from reflection inputs.
        /// </summary>
        Codegen,

        /// <summary>
        /// Validate the shader manifest.
        /// </summary>
        Validate
    }
}
