namespace helengine.editor {
    /// <summary>
    /// Represents the local build configuration persisted in `user_settings/build_config.json`.
    /// </summary>
    public sealed class EditorBuildConfigDocument {
        /// <summary>
        /// Gets or sets the per-platform local build configuration collection.
        /// </summary>
        public List<EditorBuildPlatformConfigDocument> Platforms { get; set; } = [];

        /// <summary>
        /// Gets or sets the persisted build queue entries for the current project.
        /// </summary>
        public List<EditorBuildQueueItemDocument> QueueItems { get; set; } = [];
    }
}
