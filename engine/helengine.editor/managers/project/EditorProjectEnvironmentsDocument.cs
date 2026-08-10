namespace helengine.editor {
    /// <summary>
    /// Stores the project-defined build environments persisted in `settings/environments.json`.
    /// </summary>
    public sealed class EditorProjectEnvironmentsDocument {
        /// <summary>
        /// Gets or sets the ordered environment definitions available to the project.
        /// </summary>
        public List<EditorProjectEnvironmentDefinition> Environments { get; set; } = [];
    }
}
