namespace helengine.editor {
    /// <summary>Stores the user's independently authored presets and startup selection for one platform.</summary>
    public sealed class EditorRuntimeSettingsDocument {
        /// <summary>Gets or sets the stable id of the preset applied at game startup.</summary>
        public string InitialPresetId { get; set; } = string.Empty;
        /// <summary>Gets or sets the platform's complete independent preset configurations.</summary>
        public List<EditorRuntimePresetDocument> Presets { get; set; } = [];
    }
}
