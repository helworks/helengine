namespace helengine.editor {
    /// <summary>
    /// Represents editor-local platform profile settings stored in `user_settings/profile_config.json`.
    /// </summary>
    public sealed class EditorProfileSettingsDocument {
        /// <summary>
        /// Gets or sets the persisted platform profile records ordered by supported platform list.
        /// </summary>
        public List<EditorPlatformProfileSettingsDocument> Platforms { get; set; } = [];
    }
}
