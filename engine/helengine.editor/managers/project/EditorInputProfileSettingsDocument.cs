namespace helengine.editor {
    /// <summary>
    /// Represents the persisted platform-specific input settings stored beside build, graphics, and codegen defaults.
    /// </summary>
    public sealed class EditorInputProfileSettingsDocument {
        /// <summary>
        /// Gets or sets the platform-standard action bindings used by shared UI and gameplay code.
        /// </summary>
        public EditorStandardPlatformActionSettingsDocument StandardActions { get; set; } = new EditorStandardPlatformActionSettingsDocument();
    }
}
