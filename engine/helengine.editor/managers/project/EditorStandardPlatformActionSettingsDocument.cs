namespace helengine.editor {
    /// <summary>
    /// Represents the persisted physical control bindings for standard platform-facing UI actions.
    /// </summary>
    public sealed class EditorStandardPlatformActionSettingsDocument {
        /// <summary>
        /// Gets or sets the physical control used for the standard accept action.
        /// </summary>
        public EditorInputControlSettingsDocument Accept { get; set; }

        /// <summary>
        /// Gets or sets the physical control used for the standard return action.
        /// </summary>
        public EditorInputControlSettingsDocument Return { get; set; }
    }
}
