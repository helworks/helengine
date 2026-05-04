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

        /// <summary>
        /// Gets or sets the persisted debug-build snapshot captured when the queue item was created.
        /// </summary>
        public bool DebugBuild { get; set; }

        /// <summary>
        /// Gets or sets the selected builder-provided build profile id.
        /// </summary>
        public string SelectedBuildProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided graphics profile id.
        /// </summary>
        public string SelectedGraphicsProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided build option values.
        /// </summary>
        public Dictionary<string, string> SelectedBuildOptionValues { get; set; } = [];

        /// <summary>
        /// Gets or sets the selected builder-provided graphics option values.
        /// </summary>
        public Dictionary<string, string> SelectedGraphicsOptionValues { get; set; } = [];

        /// <summary>
        /// Gets or sets the selected builder-provided codegen profile id.
        /// </summary>
        public string SelectedCodegenProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided storage profile id.
        /// </summary>
        public string SelectedStorageProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided media profile id.
        /// </summary>
        public string SelectedMediaProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided codegen option values.
        /// </summary>
        public Dictionary<string, string> SelectedCodegenOptionValues { get; set; } = [];

        /// <summary>
        /// Gets or sets the project-authored code-module identifiers enabled for this queued build.
        /// </summary>
        public List<string> SelectedCodeModuleIds { get; set; } = [];
    }
}
