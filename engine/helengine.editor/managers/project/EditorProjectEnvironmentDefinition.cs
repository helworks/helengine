namespace helengine.editor {
    /// <summary>
    /// Describes one project-defined build environment.
    /// </summary>
    public sealed class EditorProjectEnvironmentDefinition {
        /// <summary>
        /// Gets or sets the stable environment identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the environment is protected from rename and deletion.
        /// </summary>
        public bool IsProtected { get; set; }
    }
}
