namespace helengine.editor {
    /// <summary>
    /// Represents editor-local per-project settings stored in `user_settings/project.json`.
    /// </summary>
    public sealed class EditorProjectLocalSettingsDocument {
        /// <summary>
        /// Gets or sets the active project platform currently selected for editor workflows.
        /// </summary>
        public string ActivePlatform { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the platform last chosen in the viewport Play platform selector, serialized as
        /// `lastPlayPlatform`. Empty until the selector is used for the first time; it is independent of
        /// <see cref="ActivePlatform"/>, which only drives the editor preview.
        /// </summary>
        public string LastPlayPlatform { get; set; } = string.Empty;
    }
}
