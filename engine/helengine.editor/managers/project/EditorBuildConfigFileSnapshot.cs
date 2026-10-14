namespace helengine.editor {
    /// <summary>
    /// Text of the two build configuration files (`settings/build_config.json` and `user_settings/build_config.json`)
    /// read at one moment. Strings are immutable, so the snapshot can be handed to another thread, which composes the
    /// configuration from it without touching the files the editor may be writing.
    /// </summary>
    public sealed class EditorBuildConfigFileSnapshot {
        /// <summary>
        /// Initializes the snapshot from the text of both files.
        /// </summary>
        /// <param name="projectJson">Text of the project file, or null when it does not exist.</param>
        /// <param name="localJson">Text of the local file, or null when it does not exist.</param>
        public EditorBuildConfigFileSnapshot(string projectJson, string localJson) {
            ProjectJson = projectJson;
            LocalJson = localJson;
        }

        /// <summary>
        /// Gets the text of `settings/build_config.json`, or null when the file did not exist.
        /// </summary>
        public string ProjectJson { get; }

        /// <summary>
        /// Gets the text of `user_settings/build_config.json`, or null when the file did not exist.
        /// </summary>
        public string LocalJson { get; }
    }
}
