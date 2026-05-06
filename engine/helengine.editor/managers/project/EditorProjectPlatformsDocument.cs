namespace helengine.editor {
    /// <summary>
    /// Stores the project-shared supported platform identifiers persisted in `settings/platforms.json`.
    /// </summary>
    public sealed class EditorProjectPlatformsDocument {
        /// <summary>
        /// Gets or sets the platform identifiers enabled for the current project.
        /// </summary>
        public List<string> SupportedPlatforms { get; set; } = [];
    }
}
