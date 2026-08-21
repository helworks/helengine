namespace helengine.editor {
    /// <summary>
    /// Serialized per-project editor session state stored in `user_settings/editor_session.json`.
    /// </summary>
    public sealed class EditorSessionStateDocument {
        /// <summary>
        /// Gets or sets the last open scene path, stored project-relative when the scene lives inside the project.
        /// </summary>
        public string LastScenePath { get; set; }
    }
}
