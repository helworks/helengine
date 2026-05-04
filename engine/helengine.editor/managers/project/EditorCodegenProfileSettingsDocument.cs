namespace helengine.editor {
    /// <summary>
    /// Represents the persisted codegen-profile defaults used when regenerating source for one platform.
    /// </summary>
    public sealed class EditorCodegenProfileSettingsDocument {
        /// <summary>
        /// Gets or sets the selected builder-provided codegen profile id.
        /// </summary>
        public string SelectedCodegenProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the builder-provided codegen option values keyed by setting id.
        /// </summary>
        public Dictionary<string, string> SelectedOptionValues { get; set; } = [];
    }
}
