namespace helengine.editor {
    /// <summary>
    /// Represents editor-local per-project settings stored in `user_settings/project.json`.
    /// </summary>
    public sealed class EditorProjectLocalSettingsDocument {
        /// <summary>
        /// Gets or sets the active project platform currently selected for editor workflows.
        /// </summary>
        public string ActivePlatform { get; set; } = string.Empty;
    }
}
