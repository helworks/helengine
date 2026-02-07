namespace helengine.editor {
    /// <summary>
    /// Describes the behavior used when creating a new file from a template.
    /// </summary>
    public enum EditorFileTemplateKind {
        /// <summary>
        /// Plain text file created from a template string.
        /// </summary>
        Text,
        /// <summary>
        /// Shader source file intended for the shader module manager.
        /// </summary>
        Shader,
        /// <summary>
        /// Serialized material asset that may create companion shader sources.
        /// </summary>
        Material
    }
}
