namespace helengine.editor {
    /// <summary>
    /// Represents one persisted queued build entry stored in `user_settings/build_config.json`.
    /// </summary>
    public sealed class EditorBuildQueueItemDocument {
        /// <summary>
        /// Gets or sets the stable queue item identifier used to track this entry across reloads.
        /// </summary>
        public string QueueItemId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the platform identifier this queued build targets.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the project-relative scene identifiers selected for this queued build.
        /// </summary>
        public List<string> SelectedSceneIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the user-selected output directory path for this queued build.
        /// </summary>
        public string OutputDirectoryPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the persisted execution state for this queued build item.
        /// </summary>
        public EditorBuildQueueItemStatus Status { get; set; } = EditorBuildQueueItemStatus.Pending;

        /// <summary>
        /// Gets or sets the human-readable status detail associated with the current queue item state.
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;
    }
}
