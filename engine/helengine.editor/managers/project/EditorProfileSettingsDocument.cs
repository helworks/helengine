namespace helengine.editor {
    /// <summary>
    /// Represents project-shared platform profile settings aggregated from `settings/platform.<platform-id>.json` files.
    /// </summary>
    public sealed class EditorProfileSettingsDocument {
        /// <summary>
        /// Gets or sets the persisted platform profile records ordered by supported platform list.
        /// </summary>
        public List<EditorPlatformProfileSettingsDocument> Platforms { get; set; } = [];
    }
}
